using CAO.Core.Abstractions;
using CAO.Core.Compatibility;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Rollback;

/// <summary>Result of running one optimization through the full transaction.</summary>
public sealed record TransactionReport
{
    public required Guid TransactionId { get; init; }

    public required string OptimizationId { get; init; }

    public required bool Success { get; init; }

    /// <summary>Phase where the run ended (Commit on success).</summary>
    public required TransactionPhase FinalPhase { get; init; }

    public string MessageEs { get; init; } = string.Empty;

    public string? Error { get; init; }

    public bool RolledBack { get; init; }

    /// <summary>True ONLY when post-rollback state exactly matches the snapshot (P0-6).</summary>
    public bool RollbackVerified { get; init; }

    /// <summary>Cancel was requested mid-flight; the transaction finished atomically anyway (FASE 6).</summary>
    public bool CancellationDeferred { get; init; }

    /// <summary>Post-commit benchmark outcome when defined (P0-7). Failure never flips Success.</summary>
    public BenchmarkResult? Benchmark { get; init; }

    public string? BenchmarkError { get; init; }
}

/// <summary>
/// Single-optimization transaction:
/// PRECHECK → COMPATIBILITY → SNAPSHOT → APPLY → VERIFY → COMMIT, then an
/// optional POST-COMMIT BENCHMARK whose failure never invalidates the change
/// (P0-7). Any apply/verify failure rolls back and requires an EXACT match
/// against the original snapshot before reporting RollbackVerified (P0-6).
/// NotReversible optimizations are still verified (P0-5); they have no
/// automatic rollback path and failures are reported honestly.
/// </summary>
public sealed class OptimizationTransaction
{
    private readonly IOptimization _optimization;
    private readonly IRegistryAccessor _registry;
    private readonly SystemContext _context;
    private readonly IServiceManager? _services;
    private readonly Core.Interfaces.IPrivilegedCommandExecutor? _executor;
    private readonly ISnapshotStore? _snapshots;
    private readonly IHistoryLogger? _history;
    private readonly ITransactionJournal? _journal;
    private readonly CallerIdentity? _caller;

    /// <summary>Unique id persisted across transitions and used as snapshot identity (P0-3).</summary>
    public Guid TransactionId { get; }

    public OptimizationTransaction(
        IOptimization optimization,
        IRegistryAccessor registry,
        SystemContext context,
        IServiceManager? services = null,
        Core.Interfaces.IPrivilegedCommandExecutor? executor = null,
        ISnapshotStore? snapshots = null,
        IHistoryLogger? history = null,
        ITransactionJournal? journal = null,
        CallerIdentity? caller = null)
    {
        _optimization = optimization;
        _registry = registry;
        _context = context;
        _services = services;
        _executor = executor;
        _snapshots = snapshots;
        _history = history;
        _journal = journal;
        _caller = caller;
        TransactionId = Guid.NewGuid();
    }

    public async Task<TransactionReport> RunAsync(CancellationToken ct = default)
    {
        var definition = _optimization.Definition;
        var irreversible = definition.Flags.HasFlag(OptimizationFlags.NotReversible) || !definition.Reversible;

        ct.ThrowIfCancellationRequested(); // cancellation checkpoint #1

        // ---- PRECHECK ----
        PreconditionResult precondition;
        try
        {
            precondition = await _optimization.CheckPreconditionsAsync(_context, ct);
        }
        catch (Exception ex)
        {
            precondition = PreconditionResult.Fail("La comprobación de precondiciones falló: " + ex.Message);
        }

        if (!precondition.Passed)
        {
            Journal(TransactionPhase.Failed);
            Log(definition.Id, "precheck", false, null, error: precondition.ReasonEs, precondition: "failed");
            return Fail(definition.Id, TransactionPhase.Failed, precondition.ReasonEs);
        }

        // ---- COMPATIBILITY ----
        var compatibility = Rules.EvaluatePreconditions(definition, _context);
        if (!compatibility.Passed)
        {
            Journal(TransactionPhase.Failed);
            Log(definition.Id, "compatibility", false, null, error: compatibility.ReasonEs, precondition: "failed");
            return Fail(definition.Id, TransactionPhase.Failed, compatibility.ReasonEs);
        }

        // ---- SNAPSHOT (transaction-scoped identity) ----
        var snapshot = _optimization.Capture(_registry);
        _snapshots?.Save(BuildRecord(definition.Id, snapshot));

        // Cancellation checkpoint #2: last abort point with zero mutations.
        if (ct.IsCancellationRequested)
        {
            _snapshots?.Delete(TransactionId);
            Journal(TransactionPhase.CancelledBeforeApply, ErrorCodes.TxnApplyFailed);
            Log(definition.Id, "apply", false, null,
                error: "Cancelado antes de aplicar.", applyResult: null);
            return Fail(definition.Id, TransactionPhase.CancelledBeforeApply,
                "Cancelado antes de aplicar ningún cambio.", ErrorCodes.TxnApplyFailed);
        }

        var context = new OptimizationContext { Registry = _registry, Executor = _executor, Services = _services };

        // ---- RESOURCE LOCKS (FASE 15) ----
        var lease = await ResourceLockManager.Shared.AcquireAsync(_optimization.ResourceKeys, CancellationToken.None);

        // ---- APPLY (atomic: runs with CancellationToken.None) ----
        OperationResult apply;
        try
        {
            apply = await _optimization.ApplyAsync(context, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            apply = OperationResult.Fail($"Error inesperado aplicando '{definition.Id}'.", ex.Message);
        }

        if (!apply.Success)
        {
            var matchLevelApply = await SafeRollbackExactAsync(context, snapshot);
            await lease.DisposeAsync();
            Journal(matchLevelApply == SnapshotMatchLevel.ExactMatch
                ? TransactionPhase.RolledBack : TransactionPhase.Failed,
                ErrorCodes.TxnApplyFailed);
            Log(definition.Id, "apply", false,
                matchLevelApply == SnapshotMatchLevel.ExactMatch ? null : definition.Id,
                error: apply.Error ?? apply.MessageEs,
                applyResult: "failed",
                rollbackAvailable: false);

            var failed = Fail(definition.Id,
                matchLevelApply == SnapshotMatchLevel.ExactMatch ? TransactionPhase.RolledBack : TransactionPhase.Failed,
                apply.MessageEs + RollbackSuffix(matchLevelApply),
                ErrorCodes.TxnApplyFailed);
            return failed with
            {
                RolledBack = true,
                RollbackVerified = matchLevelApply == SnapshotMatchLevel.ExactMatch,
                CancellationDeferred = ct.IsCancellationRequested,
            };
        }

        // ---- VERIFY (ALWAYS — also for NotReversible, P0-5) ----
        VerificationResult verification;
        VerificationStatus status;
        try
        {
            verification = await _optimization.VerifyAsync(context, CancellationToken.None);
            status = verification.Status;
        }
        catch (Exception ex)
        {
            verification = VerificationResult.Unknown(OptimizationState.Unknown,
                "Verificación interrumpida: " + ex.Message);
            status = VerificationStatus.Unknown;
        }

        if (verification.Status is not (VerificationStatus.Passed or VerificationStatus.NotApplicable))
        {
            var verifyCode = verification.Status == VerificationStatus.Unknown
                ? ErrorCodes.VerifyUnknownState
                : ErrorCodes.VerifyFailed;
            var deferredFail = ct.IsCancellationRequested;

            if (irreversible)
            {
                // No automatic rollback exists for irreversible changes:
                // report honestly and leave evidence for recovery/manual fix.
                await lease.DisposeAsync();
                Journal(TransactionPhase.Failed, verifyCode);
                Log(definition.Id, "verify", false, definition.Id,
                    error: $"Verificación {verification.Status} en cambio irreversible.",
                    applyResult: "success",
                    verification: verification.Status.ToString().ToLowerInvariant(),
                    rollbackAvailable: false);

                var irreversibleFail = Fail(definition.Id, TransactionPhase.Failed,
                    $"La verificación devolvió {verification.Status} en un cambio irreversible: " +
                    "no se revierte automáticamente.",
                    verifyCode);
                return irreversibleFail with { CancellationDeferred = deferredFail };
            }

            var matchLevelVerify = await SafeRollbackExactAsync(context, snapshot);
            await lease.DisposeAsync();
            Journal(matchLevelVerify == SnapshotMatchLevel.ExactMatch
                ? TransactionPhase.RolledBack : TransactionPhase.Failed, verifyCode);
            Log(definition.Id, "verify", false,
                matchLevelVerify == SnapshotMatchLevel.ExactMatch ? null : definition.Id,
                error: $"Verificación {verification.Status}; cambio revertido.",
                applyResult: "success",
                verification: verification.Status.ToString().ToLowerInvariant(),
                rollbackAvailable: matchLevelVerify != SnapshotMatchLevel.ExactMatch);

            var rolledBackFail = Fail(definition.Id,
                matchLevelVerify == SnapshotMatchLevel.ExactMatch ? TransactionPhase.RolledBack : TransactionPhase.Failed,
                status == VerificationStatus.Unknown
                    ? "La verificación no pudo determinarse y el cambio se revirtió por seguridad."
                    : "La verificación falló y el cambio se revirtió.",
                verifyCode);
            return rolledBackFail with
            {
                RolledBack = true,
                RollbackVerified = matchLevelVerify == SnapshotMatchLevel.ExactMatch,
                CancellationDeferred = deferredFail,
            };
        }

        // ---- COMMIT ----
        var pendingReboot = verification.ObservedState == OptimizationState.PendingReboot;
        Journal(TransactionPhase.Commit);
        Log(definition.Id, "apply", true, definition.Id,
            applyResult: "success",
            verification: status == VerificationStatus.NotApplicable ? "not-applicable"
                : pendingReboot ? "pending-reboot" : "passed",
            rollbackAvailable: !irreversible);
        await lease.DisposeAsync();

        // ---- POST-COMMIT BENCHMARK (P0-7): failure NEVER flips Success ----
        BenchmarkResult? benchmark = null;
        string? benchmarkError = null;
        try
        {
            Journal(TransactionPhase.BenchmarkStarted);
            benchmark = await _optimization.BenchmarkAsync(CancellationToken.None);
            Journal(TransactionPhase.BenchmarkCompleted);
        }
        catch (OperationCanceledException)
        {
            benchmarkError = "benchmark cancelado";
            Journal(TransactionPhase.BenchmarkFailed);
        }
        catch (Exception ex)
        {
            benchmarkError = ex.Message;
            Journal(TransactionPhase.BenchmarkFailed);
        }

        var committed = Report(true, TransactionPhase.Commit,
            pendingReboot ? apply.MessageEs + " Requiere reinicio." : apply.MessageEs,
            benchmark, benchmarkError);
        return committed with
        {
            RollbackVerified = true,
            CancellationDeferred = ct.IsCancellationRequested,
        };

        // ---- local helpers ----

        TransactionReport Fail(string optimizationId, TransactionPhase phase, string message, string? code = null) =>
            new()
            {
                TransactionId = TransactionId,
                OptimizationId = optimizationId,
                Success = false,
                FinalPhase = phase,
                MessageEs = message,
                Error = code ?? message,
            };

        TransactionReport Report(bool success, TransactionPhase phase, string message,
            BenchmarkResult? bench = null, string? benchErr = null) => new()
        {
            TransactionId = TransactionId,
            OptimizationId = definition.Id,
            Success = success,
            FinalPhase = phase,
            MessageEs = message,
            Benchmark = bench,
            BenchmarkError = benchErr,
        };

        string RollbackSuffix(SnapshotMatchLevel level) => level switch
        {
            SnapshotMatchLevel.ExactMatch => " Se revirtió y se verificó el estado original exacto.",
            SnapshotMatchLevel.Equivalent => " Se revirtió; equivalencia parcial por captura heredada sin kind.",
            _ => " La reversión NO pudo verificarse contra el estado original.",
        };
    }

    /// <summary>
    /// Reverts a previously committed change by loading its persisted
    /// snapshot (crash recovery / batch rollback path). Returns false when
    /// no snapshot exists or the revert fails.
    /// </summary>
    public async Task<bool> RollbackCommittedAsync(CancellationToken ct = default)
    {
        var id = _optimization.Definition.Id;
        if (_snapshots is null ||
            !_snapshots.TryLoadLatestForOptimization(id, out var snapshot) ||
            snapshot is null)
        {
            return false;
        }

        var context = new OptimizationContext { Registry = _registry, Executor = _executor, Services = _services };
        try
        {
            var rollback = await _optimization.RollbackAsync(context, snapshot.State, ct);
            if (rollback.Success)
            {
                _snapshots.Delete(snapshot.Manifest.TransactionId);
                Log(id, "revert", true, null, rollbackAvailable: false);
            }
            return rollback.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Rolls back against the ORIGINAL snapshot and verifies EXACTNESS by
    /// fresh capture comparison. Deletes the stored snapshot only when the
    /// original state is confirmed restored (P0-6).
    /// </summary>
    private async Task<SnapshotMatchLevel> SafeRollbackExactAsync(
        OptimizationContext context, OptimizationSnapshot original)
    {
        try
        {
            var rollback = await _optimization.RollbackAsync(context, original, CancellationToken.None);
            if (!rollback.Success)
            {
                return SnapshotMatchLevel.Mismatch;
            }

            var fresh = _optimization.Capture(_registry);
            var level = SnapshotComparison.Compare(original, fresh);
            if (level == SnapshotMatchLevel.ExactMatch)
            {
                _snapshots?.Delete(TransactionId);
            }
            return level;
        }
        catch
        {
            return SnapshotMatchLevel.Mismatch;
        }
    }

private TransactionSnapshotRecord BuildRecord(string optimizationId, OptimizationSnapshot snapshot) => new()
    {
        Manifest = new TransactionSnapshotManifest
        {
            TransactionId = TransactionId,
            OptimizationId = optimizationId,
            DefinitionVersion = AppVersion.Semantic,
            SchemaVersion = TransactionSnapshotDefaults.SchemaVersion,
            AppVersion = AppVersion.Semantic,
            WindowsBuild = _context.WindowsBuild,
            TimestampUtc = DateTime.UtcNow,
        },
        State = snapshot,
    };

    private void Journal(TransactionPhase phase, string? errorCode = null) =>
        _journal?.Append(new TransactionEvent(TransactionId, _optimization.Definition.Id,
            DateTime.UtcNow, phase, TransactionEvent.IsTerminal(phase), errorCode,
            RequestedBySid: _caller?.Sid, RequestedByName: _caller?.Name));

    private void Log(
        string optimizationId, string operation, bool success, string? snapshotId,
        string? error = null, string precondition = "passed",
        string? applyResult = null, string? verification = null,
        bool rollbackAvailable = false, string? benchmarkSummary = null)
    {
        _history?.Log(new HistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            AppVersion = AppVersion.Semantic,
            WindowsBuild = _context.WindowsBuild,
            User = _caller is null ? Environment.UserName : (_caller.Name + " [" + _caller.Sid + "]"),
            OptimizationId = optimizationId,
            Operation = operation,
            Success = success,
            Precondition = precondition,
            SnapshotId = snapshotId,
            ApplyResult = applyResult,
            Verification = verification,
            RollbackAvailable = rollbackAvailable,
            BenchmarkSummary = benchmarkSummary,
            Error = error,
        });
    }
}

/// <summary>
/// Multi-optimization transaction: apply one by one verifying each; when an
/// application fails, stop and roll back committed changes whose risk allows
/// automatic recovery. Never continues blindly after a failure.
/// </summary>
public sealed class MultiOptimizationTransaction
{
    private readonly IReadOnlyList<IOptimization> _optimizations;
    private readonly SystemContext _context;
    private readonly Func<IOptimization, OptimizationTransaction> _factory;

    public MultiOptimizationTransaction(
        IReadOnlyList<IOptimization> optimizations,
        IRegistryAccessor registry,
        SystemContext context,
        IServiceManager? services = null,
        Core.Interfaces.IPrivilegedCommandExecutor? executor = null,
        ISnapshotStore? snapshots = null,
        IHistoryLogger? history = null,
        ITransactionJournal? journal = null,
        CallerIdentity? caller = null)
        : this(optimizations, context, o => new OptimizationTransaction(
            o, registry, context, services, executor, snapshots, history, journal))
    {
    }

    public MultiOptimizationTransaction(
        IReadOnlyList<IOptimization> optimizations,
        SystemContext context,
        Func<IOptimization, OptimizationTransaction> transactionFactory)
    {
        _optimizations = optimizations;
        _context = context;
        _factory = transactionFactory;
    }

    public async Task<IReadOnlyList<TransactionReport>> RunAsync(CancellationToken ct = default)
    {
        var reports = new List<TransactionReport>();
        var committed = new List<IOptimization>();

        foreach (var optimization in _optimizations)
        {
            ct.ThrowIfCancellationRequested();

            var report = await _factory(optimization).RunAsync(ct);
            reports.Add(report);

            if (report.Success)
            {
                committed.Add(optimization);
                continue;
            }

            foreach (var done in Enumerable.Reverse(committed))
            {
                if (done.Definition.Risk is not (RiskLevel.Safe or RiskLevel.Low))
                {
                    continue; // manual rollback for high-risk changes
                }

                await _factory(done).RollbackCommittedAsync(ct);
            }

            break;
        }

        return reports;
    }
}
