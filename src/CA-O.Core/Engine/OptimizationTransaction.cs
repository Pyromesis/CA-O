using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Engine;

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
}

/// <summary>
/// Single-optimization transaction (spec 123):
/// PRECHECK -> SNAPSHOT -> APPLY -> VERIFY -> COMMIT, with automatic
/// ROLLBACK whenever apply or verification fails. The snapshot is persisted
/// BEFORE mutating so a crash mid-apply still leaves a recovery path
/// (spec 122: never assume apply == success because the process died).
/// </summary>
public sealed class OptimizationTransaction
{
    private readonly IOptimization _optimization;
    private readonly IRegistryAccessor _registry;
    private readonly SystemContext _context;
    private readonly IServiceManager? _services;
    private readonly IProcessRunner? _process;
    private readonly ISnapshotStore? _snapshots;
    private readonly IHistoryLogger? _history;

    public OptimizationTransaction(
        IOptimization optimization,
        IRegistryAccessor registry,
        SystemContext context,
        IServiceManager? services = null,
        IProcessRunner? process = null,
        ISnapshotStore? snapshots = null,
        IHistoryLogger? history = null)
    {
        _optimization = optimization;
        _registry = registry;
        _context = context;
        _services = services;
        _process = process;
        _snapshots = snapshots;
        _history = history;
    }

    public async Task<TransactionReport> RunAsync(CancellationToken ct = default)
    {
        var definition = _optimization.Definition;

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
            Log(definition.Id, "precheck", false, null, error: precondition.ReasonEs, precondition: "failed");
            return new TransactionReport
            {
                OptimizationId = definition.Id,
                Success = false,
                FinalPhase = TransactionPhase.Failed,
                MessageEs = precondition.ReasonEs,
            };
        }

        // ---- SNAPSHOT (persisted before any mutation; crash-safe) ----
        var snapshot = _optimization.Capture(_registry);
        _snapshots?.Save(definition.Id, snapshot);

        var context = new OptimizationContext { Registry = _registry, Process = _process, Services = _services };

        // ---- APPLY ----
        OperationResult apply;
        try
        {
            apply = await _optimization.ApplyAsync(context, ct);
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
            var rolledBack = await SafeRollbackAsync(context, snapshot, ct);
            Log(definition.Id, "apply", false, rolledBack ? definition.Id : null,
                error: apply.Error ?? apply.MessageEs,
                applyResult: "failed",
                rollbackAvailable: false);
            return new TransactionReport
            {
                OptimizationId = definition.Id,
                Success = false,
                FinalPhase = rolledBack ? TransactionPhase.RolledBack : TransactionPhase.Failed,
                MessageEs = apply.MessageEs + (rolledBack ? " Se revirtió el cambio." : " La reversión falló; revise el historial."),
                Error = apply.Error,
                RolledBack = true,
            };
        }

        // ---- VERIFY ----
        if (definition.Flags.HasFlag(OptimizationFlags.NotReversible))
        {
            Log(definition.Id, "apply", true, null,
                applyResult: "success", verification: "skipped", rollbackAvailable: false);
            return new TransactionReport
            {
                OptimizationId = definition.Id,
                Success = true,
                FinalPhase = TransactionPhase.Commit,
                MessageEs = apply.MessageEs,
            };
        }

        VerificationResult verification;
        try
        {
            verification = await _optimization.VerifyAsync(context, ct);
        }
        catch (Exception ex)
        {
            verification = VerificationResult.Failed(OptimizationState.Unknown, "Verificación interrumpida: " + ex.Message);
        }

        if (!verification.Verified)
        {
            var rolledBack = await SafeRollbackAsync(context, snapshot, ct);
            Log(definition.Id, "verify", false, rolledBack ? null : definition.Id,
                error: "Verificación fallida; cambio revertido.",
                applyResult: "success",
                verification: "failed",
                rollbackAvailable: !rolledBack);
            return new TransactionReport
            {
                OptimizationId = definition.Id,
                Success = false,
                FinalPhase = rolledBack ? TransactionPhase.RolledBack : TransactionPhase.Failed,
                MessageEs = "La verificación falló y el cambio se revirtió.",
                Error = verification.MessageEs,
                RolledBack = true,
            };
        }

        // ---- COMMIT ----
        var pendingReboot = verification.ObservedState == OptimizationState.PendingReboot;
        Log(definition.Id, "apply", true, definition.Id,
            applyResult: "success",
            verification: pendingReboot ? "pending-reboot" : "passed",
            rollbackAvailable: true);

        return new TransactionReport
        {
            OptimizationId = definition.Id,
            Success = true,
            FinalPhase = TransactionPhase.Commit,
            MessageEs = pendingReboot
                ? apply.MessageEs + " Requiere reinicio."
                : apply.MessageEs,
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

        var context = new OptimizationContext { Registry = _registry, Process = _process, Services = _services };
        try
        {
            var rollback = await _optimization.RollbackAsync(context, snapshot, ct);
            if (rollback.Success)
            {
                _snapshots.Delete(id);
                Log(id, "revert", true, null, applyResult: null, verification: null, rollbackAvailable: false);
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
            if (rollback.Success)
            {
                _snapshots?.Delete(_optimization.Definition.Id);
            }
            return rollback.Success;
        }
        catch
        {
            return false;
        }
    }

    private void Log(
        string optimizationId, string operation, bool success, string? snapshotId,
        string? error = null, string precondition = "passed",
        string? applyResult = null, string? verification = null, bool rollbackAvailable = false)
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
        IProcessRunner? process = null,
        ISnapshotStore? snapshots = null,
        IHistoryLogger? history = null)
        : this(optimizations, context, o => new OptimizationTransaction(
            o, registry, context, services, process, snapshots, history))
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
