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
    private readonly Core.Interfaces.IPrivilegedCommandExecutor? _executor;
    private readonly ISystemContextProvider? _contextProvider;
    private bool _restorePointCreatedThisSession;

    public OptimizationEngine(
        IRegistryAccessor registry,
        IRestorePointService restorePoints,
        ISnapshotStore snapshots,
        IHistoryLogger history,
        IServiceManager? services = null,
        Core.Interfaces.IPrivilegedCommandExecutor? executor = null,
        ISystemContextProvider? contextProvider = null,
        CAO.Core.Rollback.ITransactionJournal? journal = null,
        Func<bool>? hasPendingRecovery = null,
        ISettingsStore? settings = null)
    {
        _registry = registry;
        _restorePoints = restorePoints;
        _snapshots = snapshots;
        _history = history;
        _services = services;
        _executor = executor;
        _contextProvider = contextProvider;
        _journal = journal;
        _hasPendingRecovery = hasPendingRecovery;
        _settings = settings;
    }

    private readonly CAO.Core.Rollback.ITransactionJournal? _journal;
    private readonly Func<bool>? _hasPendingRecovery;
    private readonly ISettingsStore? _settings;

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

        // FASE 12: no new dangerous mutations while a recovery is pending.
        if (_hasPendingRecovery?.Invoke() == true)
        {
            return OperationResult.Fail(
                "Se detectó una operación incompleta. El sistema está en modo recuperación.",
                ErrorCodes.TxnRecoveryPending);
        }

        // FASE 39: SAFE MODE refuses mutations everywhere (service included).
        if (_settings?.Load().Ui.ReadOnlyMode == true)
        {
            return OperationResult.Fail(
                "Modo de solo lectura activo: diagnóstico y benchmark disponibles, mutaciones deshabilitadas.",
                ErrorCodes.SecReadOnlyMode);
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
            Resolve(optimizationId), _registry, context, _services, _executor, _snapshots, _history, _journal);
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

        if (!_snapshots.TryLoadLatestForOptimization(optimizationId, out var record) || record is null)
        {
            return OperationResult.Fail("No hay snapshot guardado para esta optimizaci├│n.", "no-snapshot");
        }

        var context = new OptimizationContext { Registry = _registry, Executor = _executor, Services = _services };
        OperationResult result;
        try
        {
            result = await optimization.RevertAsync(context, record.State, ct);
        }
        catch (Exception ex)
        {
            result = OperationResult.Fail($"Error inesperado revirtiendo '{optimizationId}'.", ex.Message);
        }

        if (result.Success)
        {
            _snapshots.Delete(record.Manifest.TransactionId);
        }

        LogLegacy(optimizationId, "revert", result.Success, record.State, error: result.Error);
        return result;
    }

    /// <summary>Persists a fresh snapshot under a NEW transaction identity (P0-3).</summary>
    public SnapshotDescriptor CaptureSnapshot(string optimizationId)
    {
        var optimization = Resolve(optimizationId);
        PrepareServiceAwareOptimization(optimization);
        var snapshot = optimization.Capture(_registry);
        var txid = Guid.NewGuid();
        _snapshots.Save(new CAO.Core.Rollback.TransactionSnapshotRecord
        {
            Manifest = new CAO.Core.Rollback.TransactionSnapshotManifest
            {
                TransactionId = txid,
                OptimizationId = optimizationId,
                DefinitionVersion = AppVersion.Semantic,
                SchemaVersion = CAO.Core.Rollback.TransactionSnapshotDefaults.SchemaVersion,
                AppVersion = AppVersion.Semantic,
                WindowsBuild = 0,
                TimestampUtc = DateTime.UtcNow,
            },
            State = snapshot,
        });
        return new SnapshotDescriptor(txid.ToString("D"), snapshot.TimestampUtc, snapshot.Registry.Count);
    }

    /// <summary>Runs the VERIFY phase against live system state.</summary>
    public async Task<VerificationResult> VerifyAsync(string optimizationId, CancellationToken ct = default)
    {
        var optimization = Resolve(optimizationId);
        PrepareServiceAwareOptimization(optimization);
        var context = new OptimizationContext { Registry = _registry, Executor = _executor, Services = _services };
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
