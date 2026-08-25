using System.Security;
using System.Security.Principal;
using CAO.Core.Abstractions;
using CAO.Core.Catalog;
using CAO.Core.Rollback;
using CAO.Core.Optimizations.Performance;
using CAO.Shared;

namespace CAO.Core.Engine;

/// <summary>
/// Orchestrates the optimization lifecycle through the transactional model
/// (PRECHECK -> SNAPSHOT -> APPLY -> VERIFY -> COMMIT, rollback on failure).
/// The UI never calls this directly: privileged operations arrive over an
/// authenticated IPC channel to the isolated service.
/// </summary>
public sealed class OptimizationEngine
{
    private readonly IRegistryAccessor _registry;
    private readonly IRestorePointService _restorePoints;
    private readonly ISnapshotStore _snapshots;
    private readonly IHistoryLogger _history;
    private readonly IServiceManager? _services;
    private readonly IProcessRunner? _process;
    private readonly ISystemContextProvider? _contextProvider;
    private bool _restorePointCreatedThisSession;

    public OptimizationEngine(
        IRegistryAccessor registry,
        IRestorePointService restorePoints,
        ISnapshotStore snapshots,
        IHistoryLogger history,
        IServiceManager? services = null,
        IProcessRunner? process = null,
        ISystemContextProvider? contextProvider = null)
    {
        _registry = registry;
        _restorePoints = restorePoints;
        _snapshots = snapshots;
        _history = history;
        _services = services;
        _process = process;
        _contextProvider = contextProvider;
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

    /// <summary>Applies one optimization with all safety rails (transactional).</summary>
    public async Task<OperationResult> ApplyAsync(string optimizationId, CancellationToken ct = default)
    {
        if (!IsRunningAsAdmin())
        {
            return OperationResult.Fail("Se requieren permisos de administrador para aplicar cambios.", "not-admin");
        }

        // One restore point per session is enough; never block on failure.
        string? backupWarning = null;
        if (!_restorePointCreatedThisSession)
        {
            var (ok, reason) = await _restorePoints.CreateAsync("CA-O 2.0 ΓÇö antes de optimizar", ct);
            if (ok)
            {
                _restorePointCreatedThisSession = true;
            }
            else
            {
                backupWarning = reason;
            }
        }

        var context = await GetContextAsync();
        var transaction = new OptimizationTransaction(
            Resolve(optimizationId), _registry, context, _services, _process, _snapshots, _history);
        var report = await transaction.RunAsync(ct);

        return report.Success
            ? AppendWarning(new OperationResult(true, report.MessageEs), backupWarning)
            : OperationResult.Fail(report.MessageEs, report.Error ?? report.FinalPhase.ToString());
    }

    public async Task<OperationResult> RevertAsync(string optimizationId, CancellationToken ct = default)
    {
        if (!IsRunningAsAdmin())
        {
            return OperationResult.Fail("Se requieren permisos de administrador para revertir cambios.", "not-admin");
        }

        var optimization = Resolve(optimizationId);
        PrepareServiceAwareOptimization(optimization);

        if (!_snapshots.TryLoad(optimizationId, out var snapshot))
        {
            return OperationResult.Fail("No hay snapshot guardado para esta optimizaci├│n.", "no-snapshot");
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

        LogLegacy(optimizationId, "revert", result.Success, snapshot, error: result.Error);
        return result;
    }

    /// <summary>Persists a fresh snapshot without changing anything.</summary>
    public SnapshotDescriptor CaptureSnapshot(string optimizationId)
    {
        var optimization = Resolve(optimizationId);
        PrepareServiceAwareOptimization(optimization);
        var snapshot = optimization.Capture(_registry);
        _snapshots.Save(optimizationId, snapshot);
        return new SnapshotDescriptor(optimizationId, snapshot.TimestampUtc, snapshot.Registry.Count);
    }

    /// <summary>Runs the VERIFY phase against live system state.</summary>
    public async Task<VerificationResult> VerifyAsync(string optimizationId, CancellationToken ct = default)
    {
        var optimization = Resolve(optimizationId);
        PrepareServiceAwareOptimization(optimization);
        var context = new OptimizationContext { Registry = _registry, Process = _process, Services = _services };
        return await optimization.VerifyAsync(context, ct);
    }

    private async Task<SystemContext> GetContextAsync() =>
        _contextProvider is not null
            ? await _contextProvider.GetAsync()
            : SystemContextFactory.Default();

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

    /// <summary>Legacy log shape used by non-transactional paths (detect/revert).</summary>
    private void LogLegacy(string id, string operation, bool success, OptimizationSnapshot snapshot, string? error = null)
    {
        _history.Log(new HistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            AppVersion = AppVersion.Semantic,
            User = Environment.UserName,
            OptimizationId = id,
            Operation = operation,
            Success = success,
            PreviousState = SnapshotSummary(snapshot),
            Error = error,
        });
    }

    private static string? SnapshotSummary(OptimizationSnapshot snapshot) =>
        snapshot.Registry.Count == 0 ? null :
        string.Join("; ", snapshot.Registry.Select(e =>
            $"{e.KeyPath}\\{e.ValueName}={(e.Existed ? e.Value?.ToString() ?? "(empty)" : "(absent)")}"));

    private static OperationResult AppendWarning(OperationResult result, string? warning) =>
        warning is null ? result : new OperationResult(result.Success, result.MessageEs + " (Aviso: " + warning + ")", result.Error);
}
