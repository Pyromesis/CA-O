using CAO.Core.Abstractions;
using CAO.Core.Compatibility;
using CAO.Shared;

namespace CAO.Core.Rollback;

/// <summary>Result of running one optimization through the full transaction.</summary>
public sealed record TransactionReport
{
    public required string OptimizationId { get; init; }

    public required bool Success { get; init; }

    /// <summary>Phase where the run ended (Commit on success).</summary>
    public required TransactionPhase FinalPhase { get; init; }

    public string MessageEs { get; init; } = string.Empty;

    public string? Error { get; init; }

    public bool RolledBack { get; init; }

    /// <summary>True when a post-rollback re-detect confirmed the original state returned.</summary>
    public bool RollbackVerified { get; init; }

    /// <summary>Cancel was requested mid-flight; the transaction finished atomically anyway (FASE 6).</summary>
    public bool CancellationDeferred { get; init; }

    /// <summary>Benchmark outcome when the optimization defines one; null otherwise.</summary>
    public BenchmarkResult? Benchmark { get; init; }
}

/// <summary>
/// Single-optimization transaction (spec 11/123):
/// PRECHECK -> COMPATIBILITY -> SNAPSHOT -> APPLY -> VERIFY -> BENCHMARK ->
/// COMMIT, with automatic ROLLBACK + ROLLBACK-VERIFY whenever apply or
/// verification fails. The snapshot is persisted BEFORE mutating so a crash
/// mid-apply still leaves a recovery path (spec 122: never assume apply ==
/// success because the process died).
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

    public OptimizationTransaction(
        IOptimization optimization,
        IRegistryAccessor registry,
        SystemContext context,
        IServiceManager? services = null,
        Core.Interfaces.IPrivilegedCommandExecutor? executor = null,
        ISnapshotStore? snapshots = null,
        IHistoryLogger? history = null)
    {
        _optimization = optimization;
        _registry = registry;
        _context = context;
        _services = services;
        _executor = executor;
        _snapshots = snapshots;
        _history = history;
    }

    public async Task<TransactionReport> RunAsync(CancellationToken ct = default)
    {
        var definition = _optimization.Definition;

        // ---- Cancellation checkpoint #1 (FASE 6): before any work ----
        ct.ThrowIfCancellationRequested();

        // ---- PRECHECK (optimization-specific gates) ----
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
            Log(definition.Id, "precheck", false, null, error: precondition.ReasonEs, precondition: "failed");
            return new TransactionReport
            {
                OptimizationId = definition.Id,
                Success = false,
                FinalPhase = TransactionPhase.Failed,
                MessageEs = precondition.ReasonEs,
            };
        }

        // ---- COMPATIBILITY (context-level rules; always enforced) ----
        var compatibility = Rules.EvaluatePreconditions(definition, _context);
        if (!compatibility.Passed)
        {
            Log(definition.Id, "compatibility", false, null, error: compatibility.ReasonEs, precondition: "failed");
            return new TransactionReport
            {
                OptimizationId = definition.Id,
                Success = false,
                FinalPhase = TransactionPhase.Failed,
                MessageEs = compatibility.ReasonEs,
            };
        }

        // ---- SNAPSHOT (persisted before any mutation; crash-safe) ----
        var snapshot = _optimization.Capture(_registry);
        _snapshots?.Save(definition.Id, snapshot);

        // ---- Cancellation checkpoint #2 (FASE 6): last point where an
        // abort leaves zero mutations. After this line the token is ignored
        // until the mutation completes atomically; a pending cancellation is
        // honoured by finishing apply→verify→(rollback|commit), never by
        // tearing the change in half.
        if (ct.IsCancellationRequested)
        {
            _snapshots?.Delete(definition.Id);
            Log(definition.Id, "apply", false, null, error: "Cancelado antes de aplicar.",
                precondition: "passed", applyResult: null);
            return new TransactionReport
            {
                OptimizationId = definition.Id,
                Success = false,
                FinalPhase = TransactionPhase.Failed,
                MessageEs = "Cancelado antes de aplicar ningún cambio.",
                Error = ErrorCodes.TxnApplyFailed,
            };
        }

        var context = new OptimizationContext { Registry = _registry, Executor = _executor, Services = _services };

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
            var rollbackVerified = await SafeRollbackAsync(context, snapshot, CancellationToken.None);
        var deferred = ct.IsCancellationRequested;
            Log(definition.Id, "apply", false, rollbackVerified ? null : definition.Id,
                error: apply.Error ?? apply.MessageEs,
                applyResult: "failed",
                rollbackAvailable: false);
            return new TransactionReport
            {
                OptimizationId = definition.Id,
                Success = false,
                FinalPhase = rollbackVerified ? TransactionPhase.RolledBack : TransactionPhase.Failed,
                MessageEs = apply.MessageEs + (rollbackVerified
                    ? " Se revirtió el cambio y se verificó la reversión."
                    : " La reversión falló o no pudo verificarse; revise el historial."),
                Error = apply.Error,
                RolledBack = true,
                RollbackVerified = rollbackVerified,
                CancellationDeferred = deferred,
            };
        }

        // ---- VERIFY (strict; Unknown is never success) ----
        if (!definition.Flags.HasFlag(OptimizationFlags.NotReversible))
        {
            VerificationResult verification;
            try
            {
                verification = await _optimization.VerifyAsync(context, CancellationToken.None);
            }
            catch (Exception ex)
            {
                verification = VerificationResult.Unknown(OptimizationState.Unknown, "Verificación interrumpida: " + ex.Message);
            }

            if (verification.Status != VerificationStatus.Passed)
            {
                var rollbackVerified = await SafeRollbackAsync(context, snapshot, CancellationToken.None);
        var deferred = ct.IsCancellationRequested;
                var code = verification.Status == VerificationStatus.Unknown
                    ? ErrorCodes.VerifyUnknownState
                    : ErrorCodes.VerifyFailed;
                Log(definition.Id, "verify", false, rollbackVerified ? null : definition.Id,
                    error: $"Verificación {verification.Status}; cambio revertido.",
                    applyResult: "success",
                    verification: verification.Status.ToString().ToLowerInvariant(),
                    rollbackAvailable: !rollbackVerified);
                return new TransactionReport
                {
                    OptimizationId = definition.Id,
                    Success = false,
                    FinalPhase = rollbackVerified ? TransactionPhase.RolledBack : TransactionPhase.Failed,
                    MessageEs = verification.Status == VerificationStatus.Unknown
                        ? "La verificación no pudo determinarse y el cambio se revirtió por seguridad."
                        : "La verificación falló y el cambio se revirtió.",
                    Error = code + ": " + verification.MessageEs,
                    RolledBack = true,
                    RollbackVerified = rollbackVerified,
                    CancellationDeferred = deferred,
                };
            }
        }

        // ---- BENCHMARK (optional hook) ----
        BenchmarkResult? benchmark = null;
        try
        {
            benchmark = await _optimization.BenchmarkAsync(CancellationToken.None);
        }
        catch
        {
            benchmark = null; // benchmark failure never breaks the transaction
        }

        // ---- COMMIT ----
        var pendingReboot = definition.Flags.HasFlag(OptimizationFlags.NotReversible)
            ? false
            : DetectAfterApply(context.Registry) is OptimizationState.PendingReboot;

        Log(definition.Id, "apply", true, definition.Id,
            applyResult: "success",
            verification: definition.Flags.HasFlag(OptimizationFlags.NotReversible)
                ? "skipped"
                : pendingReboot ? "pending-reboot" : "passed",
            rollbackAvailable: true,
            benchmarkSummary: DescribeBenchmark(benchmark));

        return new TransactionReport
        {
            OptimizationId = definition.Id,
            Success = true,
            FinalPhase = TransactionPhase.Commit,
            MessageEs = pendingReboot ? apply.MessageEs + " Requiere reinicio." : apply.MessageEs,
            RollbackVerified = true,
            CancellationDeferred = ct.IsCancellationRequested,
            Benchmark = benchmark,
        };
    }

    /// <summary>
    /// Reverts a previously committed change by loading its persisted
    /// snapshot (crash recovery and batch rollback path).
    /// </summary>
    public async Task<bool> RollbackCommittedAsync(CancellationToken ct = default)
    {
        var id = _optimization.Definition.Id;
        if (_snapshots is null || !_snapshots.TryLoad(id, out var snapshot))
        {
            return false;
        }

        var context = new OptimizationContext { Registry = _registry, Executor = _executor, Services = _services };
        try
        {
            var rollback = await _optimization.RollbackAsync(context, snapshot, ct);
            if (rollback.Success)
            {
                _snapshots.Delete(id);
                Log(id, "revert", true, null, rollbackAvailable: false);
            }
            return rollback.Success;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> SafeRollbackAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            var rollback = await _optimization.RollbackAsync(context, snapshot, ct);
            if (!rollback.Success)
            {
                return false;
            }

            // VERIFY ROLLBACK: re-detect must NOT report the applied state.
            var observed = _optimization.Detect(_registry);
            if (observed == OptimizationState.AppliedByCao)
            {
                return false;
            }

            _snapshots?.Delete(_optimization.Definition.Id);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private OptimizationState DetectAfterApply(IRegistryAccessor registry)
    {
        try
        {
            return _optimization.Detect(registry);
        }
        catch
        {
            return OptimizationState.Unknown;
        }
    }

    private static string? DescribeBenchmark(BenchmarkResult? benchmark) =>
        benchmark is null ? null :
        benchmark.FrameTimes is not null
            ? $"fps={benchmark.FrameTimes.AverageFps:0.0}; p99ft={benchmark.FrameTimes.P99FrameTimeMs:0.00}ms"
            : benchmark.Metric is null ? "run" : $"{benchmark.MetricName}={benchmark.Metric:0.##}{benchmark.MetricUnit}";

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
            User = Environment.UserName,
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
/// Multi-optimization transaction (spec 124): apply one by one verifying
/// each; when an application fails, stop and roll back the changes this
/// batch already committed whose risk allows automatic recovery. Never
/// continue blindly applying the remaining list.
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
        IHistoryLogger? history = null)
        : this(optimizations, context, o => new OptimizationTransaction(
            o, registry, context, services, executor, snapshots, history))
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
                    continue; // offer manual rollback instead of touching high-risk state twice
                }

                await _factory(done).RollbackCommittedAsync(ct);
            }

            break;
        }

        return reports;
    }
}
