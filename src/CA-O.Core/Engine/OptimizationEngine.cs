using System.Security;
using System.Security.Principal;
using CAO.Core.Abstractions;
using CAO.Core.Catalog;
using CAO.Core.Optimizations.Performance;
using CAO.Shared;

namespace CAO.Core.Engine;

/// <summary>
/// Orchestrates the full lifecycle: detect -> restore point -> snapshot ->
/// apply -> verify (Detect) -> persist snapshot -> log. Reverts load the
/// persisted snapshot and restore the exact previous state.
/// </summary>
public sealed class OptimizationEngine
{
    private readonly IRegistryAccessor _registry;
    private readonly IRestorePointService _restorePoints;
    private readonly ISnapshotStore _snapshots;
    private readonly IHistoryLogger _history;
    private readonly IServiceManager? _services;
    private readonly IProcessRunner? _process;
    private bool _restorePointCreatedThisSession;

    public OptimizationEngine(
        IRegistryAccessor registry,
        IRestorePointService restorePoints,
        ISnapshotStore snapshots,
        IHistoryLogger history,
        IServiceManager? services = null,
        IProcessRunner? process = null)
    {
        _registry = registry;
        _restorePoints = restorePoints;
        _snapshots = snapshots;
        _history = history;
        _services = services;
        _process = process;
    }

    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public IReadOnlyList<OptimizationDefinition> Definitions =>
        OptimizationCatalog.All.Select(o => o.Definition).ToList();

    public OptimizationState Detect(string optimizationId, IOptimization? instance = null)
    {
        var optimization = Resolve(optimizationId, instance);
        PrepareServiceAwareOptimization(optimization);
        return optimization.Detect(_registry);
    }

    /// <summary>Applies one optimization with all safety rails.</summary>
    public async Task<OperationResult> ApplyAsync(string optimizationId, CancellationToken ct = default)
    {
        var optimization = Resolve(optimizationId);
        PrepareServiceAwareOptimization(optimization);

        if (!IsRunningAsAdmin())
        {
            return OperationResult.Fail("Se requieren permisos de administrador para aplicar cambios.", "not-admin");
        }

        // One restore point per session is enough; never block on failure.
        string? backupWarning = null;
        if (!_restorePointCreatedThisSession)
        {
            var (ok, reason) = await _restorePoints.CreateAsync("CA-O 2.0 — antes de optimizar", ct);
            if (ok)
            {
                _restorePointCreatedThisSession = true;
            }
            else
            {
                backupWarning = reason;
            }
        }

        var snapshot = optimization.Capture(_registry);
        var context = new OptimizationContext { Registry = _registry, Process = _process, Services = _services };

        OperationResult result;
        try
        {
            result = await optimization.ApplyAsync(context, ct);
        }
        catch (Exception ex)
        {
            result = OperationResult.Fail($"Error inesperado aplicando '{optimizationId}'.", ex.Message);
        }

        // Verify: Detect must now report applied (except maintenance actions).
        if (result.Success && optimization.Definition.Flags.HasFlag(OptimizationFlags.NotReversible))
        {
            // Maintenance actions have no post-state to verify.
        }
        else if (result.Success)
        {
            var after = optimization.Detect(_registry);
            if (after is not (OptimizationState.AppliedByCao or OptimizationState.Unknown or OptimizationState.PendingReboot))
            {
                // Roll back immediately: verification failed.
                await optimization.RevertAsync(context, snapshot, ct);
                Log(optimizationId, "apply", false, snapshot, error: "Verificación fallida; se revirtió automáticamente.");
                return OperationResult.Fail("La verificación posterior falló y el cambio se revirtió.", "verify-failed");
            }
        }

        if (result.Success)
        {
            _snapshots.Save(optimizationId, snapshot);
        }

        Log(optimizationId, "apply", result.Success, snapshot, error: result.Error);
        return backupWarning is not null && result.Success
            ? new OperationResult(true, result.MessageEs + " (Aviso: " + backupWarning + ")", result.Error)
            : result;
    }

    public async Task<OperationResult> RevertAsync(string optimizationId, CancellationToken ct = default)
    {
        var optimization = Resolve(optimizationId);

        if (!IsRunningAsAdmin())
        {
            return OperationResult.Fail("Se requieren permisos de administrador para revertir cambios.", "not-admin");
        }

        if (!_snapshots.TryLoad(optimizationId, out var snapshot))
        {
            return OperationResult.Fail("No hay snapshot guardado para esta optimización.", "no-snapshot");
        }

        var context = new OptimizationContext { Registry = _registry, Process = _process, Services = _services };
        OperationResult result;
        try
        {
            result = await optimization.RevertAsync(context, snapshot, ct);
        }
        catch (Exception ex)
        {
            result = OperationResult.Fail($"Error inesperado revirtiendo '{optimizationId}'.", ex.Message);
        }

        if (result.Success)
        {
            _snapshots.Delete(optimizationId);
        }
        Log(optimizationId, "revert", result.Success, snapshot, error: result.Error);
        return result;
    }

    private IOptimization Resolve(string optimizationId, IOptimization? instance = null)
    {
        if (instance is not null) return instance;
        var found = OptimizationCatalog.All.FirstOrDefault(o =>
            o.Definition.Id.Equals(optimizationId, StringComparison.OrdinalIgnoreCase));
        return found ?? throw new InvalidOperationException($"Unknown optimization '{optimizationId}'");
    }

    private void PrepareServiceAwareOptimization(IOptimization optimization)
    {
        if (optimization is IServiceAwareOptimization serviceAware && _services is not null)
        {
            serviceAware.SetObservedStartType(_services.GetStartType(DisableSearchIndexing.ServiceName));
        }
    }

    private void Log(string id, string action, bool success, OptimizationSnapshot snapshot, string? error = null)
    {
        _history.Log(new HistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            User = Environment.UserName,
            OptimizationId = id,
            Action = action,
            Success = success,
            PreviousState = SnapshotSummary(snapshot),
            Error = error,
        });
    }

    private static string? SnapshotSummary(OptimizationSnapshot snapshot) =>
        snapshot.Registry.Count == 0 ? null :
        string.Join("; ", snapshot.Registry.Select(e =>
            $"{e.KeyPath}\\{e.ValueName}={(e.Existed ? e.Value?.ToString() ?? "(empty)" : "(absent)")}"));
}
