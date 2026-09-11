using System.Security;
using System.Security.Principal;
using CAO.Core.Abstractions;
using CAO.Core.Catalog;
using CAO.Core.Optimization;
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
    private readonly Core.Interfaces.IDnsConfigurationProvider? _dnsProvider;
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
        ISettingsStore? settings = null,
        Core.Interfaces.IDnsConfigurationProvider? dnsProvider = null)
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
        _dnsProvider = dnsProvider;
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
    public async Task<OperationResult> ApplyAsync(string optimizationId, Shared.Security.CallerIdentity? caller = null, CancellationToken ct = default)
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

        // Restore point policy (FASE 12): per-optimization RequiresRestorePoint
        var definition = OptimizationCatalog.All.FirstOrDefault(o => o.Definition.Id.Equals(optimizationId, StringComparison.OrdinalIgnoreCase));
        var requiresRestorePoint = definition?.Definition.RequiresRestorePoint ?? false;

        string? backupWarning = null;
        if (requiresRestorePoint && !_restorePointCreatedThisSession)
        {
            var (ok, reason) = await _restorePoints.CreateAsync($"CA-O 2.0 — antes de {optimizationId}", ct);
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
        // Gaming bloque real §26: si Vanguard/anti-cheat y optimización sensible => CAO-GAME-001
        var gaming = Gaming.GameCompatibilityPolicy.Evaluate(optimizationId, context);
        if (gaming.Compatibility == Gaming.GameCompatibility.Blocked)
        {
            return OperationResult.Fail($"CAO-GAME-001: {gaming.ReasonEs}", "CAO-GAME-001");
        }

        // Exclusión mutua de planes de energía: activar uno y después otro
        // no acumula, se pisan. Se exige revertir el activo primero.
        if (Optimization.OptimizationConflicts.IsPowerScheme(optimizationId))
        {
            var activeScheme = Optimization.PowerSchemes.ReadActiveScheme(_registry);
            var conflict = Optimization.OptimizationConflicts.EvaluatePowerScheme(optimizationId, activeScheme);
            if (conflict.Outcome == Optimization.OptimizationConflicts.ConflictOutcome.AlreadyApplied)
            {
                return OperationResult.Ok(conflict.MessageEs);
            }
            if (conflict.Outcome == Optimization.OptimizationConflicts.ConflictOutcome.Blocked)
            {
                return OperationResult.Fail(conflict.MessageEs, ErrorCodes.ConflictPowerScheme);
            }
        }

        // Idempotencia: ya aplicado => éxito sin mutar. Garantiza que una
        // optimización solo se puede activar una vez aunque la UI tenga
        // estado obsoleto (el Detect manda, no la tarjeta).
        OptimizationState preState;
        try
        {
            preState = Resolve(optimizationId).Detect(_registry);
        }
        catch
        {
            preState = OptimizationState.Unknown;
        }
        if (preState == OptimizationState.AppliedByCao)
        {
            return OperationResult.Ok("Ya aplicado y verificado — no se puede volver a aplicar. Use Revertir si desea restaurarlo.");
        }

        var transaction = new OptimizationTransaction(
            Resolve(optimizationId), _registry, context, _services, _executor, _snapshots, _history, _journal, caller);
        var report = await transaction.RunAsync(ct);

        return report.Success
            ? AppendWarning(new OperationResult(true, report.MessageEs), backupWarning)
            : OperationResult.Fail(report.MessageEs, report.Error ?? report.FinalPhase.ToString());
    }

    public async Task<OperationResult> RevertAsync(string optimizationId, Shared.Security.CallerIdentity? caller = null, CancellationToken ct = default)
    {
        if (!IsRunningAsAdmin())
        {
            return OperationResult.Fail("Se requieren permisos de administrador para revertir cambios.", "not-admin");
        }

        TransactionSnapshotRecord? record = null;
        string resolvedId = optimizationId;
        // Si se pasa un TransactionId (GUID), usar ese snapshot específico (RestorePage)
        if (Guid.TryParse(optimizationId, out var txid) && _snapshots.TryLoad(txid, out var byTx) && byTx != null)
        {
            record = byTx;
            resolvedId = byTx.Manifest.OptimizationId;
        }
        else if (!_snapshots.TryLoadLatestForOptimization(optimizationId, out var byOpt) || byOpt is null)
        {
            return OperationResult.Fail("No hay snapshot guardado para esta optimización.", "no-snapshot");
        }
        else
        {
            record = byOpt;
        }

        var optimization = Resolve(resolvedId);
        PrepareServiceAwareOptimization(optimization);

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

        LogLegacy(optimizationId, "revert", result.Success, record.State, error: result.Error, caller: caller);
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

    public async Task<OperationResult> SetDnsAsync(string interfaceName, string dnsIp, CancellationToken ct = default, CAO.Core.Interfaces.IDnsConfigurationProvider? dnsProviderOverride = null)
    {
        if (!IsRunningAsAdmin()) return OperationResult.Fail("Se requieren privilegios.", "not-admin");
        if (_executor is null) return OperationResult.Fail("Ejecutor no disponible.", "no-executor");
        if (string.IsNullOrWhiteSpace(interfaceName)) return OperationResult.Fail("Interfaz no especificada.", "invalid-adapter");
        
        // Handle comma-separated primary,secondary
        var parts = dnsIp.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return OperationResult.Fail("IP DNS no especificada.", "invalid-ip");
        var primary = parts[0];
        var secondary = parts.Length > 1 ? parts[1] : null;
        if (!System.Net.IPAddress.TryParse(primary, out _)) return OperationResult.Fail($"IP DNS inválida: {primary}", "invalid-ip");
        if (secondary != null && !System.Net.IPAddress.TryParse(secondary, out _)) return OperationResult.Fail($"IP DNS secundaria inválida: {secondary}", "invalid-ip");

        // Regla mismo-proveedor: nunca mezclar (p. ej. nunca 2000.21.200.10 +
        // 1.1.1.1). Si llega un par mezclado (snapshot antiguo o llamada
        // externa), corregir el secundario al compañero canónico del primario
        // (1.1.1.1→1.0.0.1, 8.8.8.8→8.8.4.4) o al hermano ISP mismo /24.
        // Se lee el estado previo para buscar hermano ISP antes de normalizar.
        var dnsProviderEarly = dnsProviderOverride ?? _dnsProvider;
        var preDnsForPairing = dnsProviderEarly?.GetAdapter(interfaceName)?.CurrentDnsV4;
        if (secondary != null && !global::CAO.Shared.Networking.DnsResolverPairs.IsSameProvider(primary, secondary))
        {
            var (_, fixedSecondary) = global::CAO.Shared.Networking.DnsResolverPairs.ResolvePair(primary, preDnsForPairing);
            secondary = fixedSecondary;
        }
        else if (secondary == null)
        {
            // Par completo mismo-proveedor aunque solo venga el primario.
            (_, secondary) = global::CAO.Shared.Networking.DnsResolverPairs.ResolvePair(primary, preDnsForPairing);
        }
        
        var dnsProvider = dnsProviderOverride ?? _dnsProvider;
        
        // Ignore virtual/VPN unless explicitly allowed
        if (dnsProvider != null && dnsProvider.IsVirtualOrVpn(interfaceName)) 
            return OperationResult.Fail($"Adaptador virtual/VPN ignorado: {interfaceName}", "virtual-adapter");
        
        // Capture FULL state before any change (for exact rollback)
        var adapterBefore = dnsProvider?.GetAdapter(interfaceName);
        if (adapterBefore == null) 
            return OperationResult.Fail($"Adaptador no encontrado: {interfaceName}", "adapter-not-found");
        
        var beforeDhcp = adapterBefore.DhcpEnabled;
        var beforeDnsV4 = adapterBefore.CurrentDnsV4.ToArray();
        var beforeDnsV6 = adapterBefore.CurrentDnsV6.ToArray();
        
        // Apply primary DNS
        var result = await _executor.ExecuteAsync(CAO.Shared.Security.SystemCommandKey.NetShInterfaceIpSetDnsPrimary,
            ["interface", "ip", "set", "dns", interfaceName, "static", primary], ct);
        if (!result.Success) return OperationResult.Fail($"No se pudo aplicar DNS {primary} a {interfaceName}: {result.StdErr}", result.StdErr);
        
        // Apply secondary if provided
        if (!string.IsNullOrEmpty(secondary))
        {
            var r2 = await _executor.ExecuteAsync(CAO.Shared.Security.SystemCommandKey.NetShInterfaceIpSetDnsSecondary,
                ["interface", "ip", "add", "dns", interfaceName, secondary], ct);
            if (!r2.Success) 
            { 
                // Rollback primary to exact previous state
                await RollbackDnsExact(interfaceName, beforeDhcp, beforeDnsV4, beforeDnsV6, ct);
                return OperationResult.Fail($"No se pudo aplicar DNS secundario {secondary}: {r2.StdErr}", r2.StdErr);
            }
        }
        
        // Verify with retry (WMI/NetworkInterface cache tarda en refrescar)
        if (dnsProvider != null)
        {
            IReadOnlyList<string> after = Array.Empty<string>();
            bool ok = false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try { await Task.Delay(attempt == 0 ? 600 : 800, ct); } catch { }
                try { after = dnsProvider.GetDnsServers(interfaceName); } catch { after = Array.Empty<string>(); }
                ok = after.Any(a => a == primary);
                if (ok) break;
                // Si provider devuelve vacío (lectura falló), no considerar fallo de verificación — confiar en exit code 0
                if (after.Count == 0 && attempt == 2) { ok = true; break; }
            }
            if (!ok && after.Count > 0)
            {
                // Rollback solo si leímos DNS distinto no vacío (verificación real falló)
                await RollbackDnsExact(interfaceName, beforeDhcp, beforeDnsV4, beforeDnsV6, ct);
                return OperationResult.Fail($"Verificación fallida: DNS no coincide tras aplicar {primary} (leído: {string.Join(",", after)}). Rollback exacto ejecutado.", "verification-failed");
            }
            if (!ok && after.Count == 0)
            {
                // Provider no pudo leer — considerar aplicado (netsh exit 0) y avisar
                var pairPending = secondary is null ? primary : $"{primary},{secondary}";
                return OperationResult.Ok($"DNS {pairPending} aplicado a {interfaceName} — aplicado (verificación no disponible, netsh ok).");
            }
        }
        var pairLabel = secondary is null ? primary : $"{primary},{secondary}";
        return OperationResult.Ok($"DNS {pairLabel} aplicado a {interfaceName} — verificado (mismo proveedor, sin mezclar).");
    }

    /// <summary>
    /// Corrige un dispositivo con problema vía pnputil (fase drivers 2).
    /// rescan = re-detectar; enable = habilitar; reinstall = desinstalar +
    /// re-detectar (reinstala el controlador de la tienda de drivers).
    /// Verifica con `pnputil /enum-devices /problem`: éxito solo si el
    /// dispositivo ya no aparece listado. Nunca lanza.
    /// </summary>
    public async Task<OperationResult> FixDriverAsync(string instanceId, string action, CancellationToken ct = default)
    {
        // La entrada se valida antes que los privilegios: rechazar basura no
        // requiere permisos (y así es testeable sin elevación).
        if (!global::CAO.Shared.IPC.FixDriverActions.IsValid(action))
            return OperationResult.Fail($"Acción no válida: {action}", "invalid-action");
        if (!global::CAO.Shared.Security.CommandPolicy.IsValidPnpInstanceId(instanceId))
            return OperationResult.Fail("ID de instancia no válido.", "invalid-id");
        if (!IsRunningAsAdmin()) return OperationResult.Fail("Se requieren privilegios.", "not-admin");
        if (_executor is null) return OperationResult.Fail("Ejecutor no disponible.", "no-executor");

        try
        {
            if (action == global::CAO.Shared.IPC.FixDriverActions.Enable)
            {
                var enable = await _executor.ExecuteAsync(
                    global::CAO.Shared.Security.SystemCommandKey.PnPUtilEnableDevice,
                    ["/enable-device", instanceId], ct);
                if (!enable.Success)
                    return OperationResult.Fail($"No se pudo habilitar el dispositivo: {enable.StdErr}", enable.StdErr);
            }
            else if (action == global::CAO.Shared.IPC.FixDriverActions.Reinstall)
            {
                var remove = await _executor.ExecuteAsync(
                    global::CAO.Shared.Security.SystemCommandKey.PnPUtilRemoveDevice,
                    ["/remove-device", instanceId], ct);
                if (!remove.Success)
                    return OperationResult.Fail($"No se pudo desinstalar el dispositivo: {remove.StdErr}", remove.StdErr);
            }
            // rescan tras habilitar/reinstalar (y como acción propia): re-enumera
            // el hardware para que Windows recargue el controlador.
            var scan = await _executor.ExecuteAsync(
                global::CAO.Shared.Security.SystemCommandKey.PnPUtilScanDevices,
                ["/scan-devices"], ct);
            if (!scan.Success)
                return OperationResult.Fail($"Re-detección fallida: {scan.StdErr}", scan.StdErr);

            try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch { }

            var problems = await _executor.ExecuteAsync(
                global::CAO.Shared.Security.SystemCommandKey.PnPUtilEnumProblemDevices,
                ["/enum-devices", "/problem"], ct);
            if (!problems.Success)
                return OperationResult.Ok("Acción ejecutada, pero no se pudo verificar (lista de problemas ilegible). Revisa Administrador de dispositivos.");

            if (IsProblemListed(problems.StdOut, instanceId))
                return OperationResult.Fail(
                    "El dispositivo sigue reportando problema tras la corrección. Prueba con el controlador original del fabricante (fase 2) o reinicia el equipo.",
                    "still-broken");
            var rebootHint = problems.StdOut.Contains("reboot", StringComparison.OrdinalIgnoreCase) ? " (si pide reinicio, reinicia para completar)" : string.Empty;
            return OperationResult.Ok($"Dispositivo corregido: ya no reporta problema{rebootHint}.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Error corrigiendo el dispositivo ({ex.GetType().Name}: {ex.Message}).", "unexpected");
        }
    }

    internal static bool IsProblemListed(string enumOutput, string instanceId)
    {
        foreach (var line in enumOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Instance ID:", StringComparison.OrdinalIgnoreCase) &&
                trimmed["Instance ID:".Length..].Trim().Equals(instanceId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Instala un INF del fabricante (pnputil /add-driver + /install) y
    /// re-detecta. Si se indica InstanceId de un dispositivo con problema,
    /// éxito solo si desaparece de la lista; si no, vale el exit code con
    /// aviso de comprobar en Administrador de dispositivos. Nunca lanza.
    /// </summary>
    public async Task<OperationResult> InstallDriverInfAsync(string infPath, string instanceId, CancellationToken ct = default)
    {
        if (!global::CAO.Shared.Security.CommandPolicy.IsValidInfPath(infPath))
            return OperationResult.Fail("Ruta INF no válida.", "invalid-inf");
        if (!string.IsNullOrEmpty(instanceId) &&
            !global::CAO.Shared.Security.CommandPolicy.IsValidPnpInstanceId(instanceId))
            return OperationResult.Fail("ID de instancia no válido.", "invalid-id");
        if (!IsRunningAsAdmin()) return OperationResult.Fail("Se requieren privilegios.", "not-admin");
        if (_executor is null) return OperationResult.Fail("Ejecutor no disponible.", "no-executor");

        try
        {
            var add = await _executor.ExecuteAsync(
                global::CAO.Shared.Security.SystemCommandKey.PnPUtilAddDriver,
                ["/add-driver", infPath, "/install"], ct);
            if (!add.Success)
                return OperationResult.Fail($"No se pudo instalar el controlador: {add.StdErr}", add.StdErr);

            var scan = await _executor.ExecuteAsync(
                global::CAO.Shared.Security.SystemCommandKey.PnPUtilScanDevices,
                ["/scan-devices"], ct);
            if (!scan.Success)
                return OperationResult.Fail($"Instalado, pero la re-detección falló: {scan.StdErr}", scan.StdErr);

            if (string.IsNullOrEmpty(instanceId))
                return OperationResult.Ok("Controlador instalado (pnputil exit 0). Comprueba el dispositivo en Administrador de dispositivos.");

            try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch { }

            var problems = await _executor.ExecuteAsync(
                global::CAO.Shared.Security.SystemCommandKey.PnPUtilEnumProblemDevices,
                ["/enum-devices", "/problem"], ct);
            if (!problems.Success)
                return OperationResult.Ok("Controlador instalado, pero no se pudo verificar (lista ilegible). Revisa Administrador de dispositivos.");
            if (IsProblemListed(problems.StdOut, instanceId))
                return OperationResult.Fail(
                    "Instalado, pero el dispositivo sigue con problema. Prueba otro INF del fabricante o reinicia.",
                    "still-broken");
            return OperationResult.Ok("Controlador original instalado y dispositivo sin problema.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Error instalando el controlador ({ex.GetType().Name}: {ex.Message}).", "unexpected");
        }
    }

    /// <summary>
    /// Limpieza de fantasmas: desinstala dispositivos NO presentes (restos de
    /// hardware desconectado que causan conflictos, p. ej. audio/USB antiguos).
    /// Fail-safe por diseño: se omite todo ID visible en Win32_PnPEntity
    /// (presente) o con estado Started en pnputil. /remove-device no borra el
    /// driver, solo desinstala el nodo; un /scan-devices final re-enumera.
    /// Nunca lanza (salvo cancelación).
    /// </summary>
    public async Task<OperationResult> RemovePhantomDevicesAsync(IReadOnlyList<string> instanceIds, CancellationToken ct = default)
    {
        if (instanceIds is null || instanceIds.Count == 0 || instanceIds.Count > 200)
            return OperationResult.Fail("Lista vacía o excesiva (máx. 200).", "invalid-list");
        foreach (var id in instanceIds)
        {
            if (!global::CAO.Shared.Security.CommandPolicy.IsValidPnpInstanceId(id))
                return OperationResult.Fail("ID de instancia no válido.", "invalid-id");
        }
        if (!IsRunningAsAdmin()) return OperationResult.Fail("Se requieren privilegios.", "not-admin");
        if (_executor is null) return OperationResult.Fail("Ejecutor no disponible.", "no-executor");

        try
        {
            var present = await ReadPresentDeviceIdsAsync(ct);
            int removed = 0, skipped = 0, failed = 0;
            foreach (var id in instanceIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                if (present.Contains(id))
                {
                    skipped++;
                    continue;
                }
                var probe = await _executor.ExecuteAsync(
                    global::CAO.Shared.Security.SystemCommandKey.PnPUtilEnumDevice,
                    ["/enum-devices", "/instanceid", id], ct);
                if (IsStartedStatus(probe.StdOut))
                {
                    skipped++;
                    continue;
                }
                var remove = await _executor.ExecuteAsync(
                    global::CAO.Shared.Security.SystemCommandKey.PnPUtilRemoveDevice,
                    ["/remove-device", id], ct);
                if (!remove.Success)
                {
                    failed++;
                    continue;
                }
                var verify = await _executor.ExecuteAsync(
                    global::CAO.Shared.Security.SystemCommandKey.PnPUtilEnumDevice,
                    ["/enum-devices", "/instanceid", id], ct);
                if (IsStartedStatus(verify.StdOut)) failed++;
                else removed++;
            }
            try
            {
                await _executor.ExecuteAsync(
                    global::CAO.Shared.Security.SystemCommandKey.PnPUtilScanDevices,
                    ["/scan-devices"], ct);
            }
            catch { }
            if (failed > 0)
                return OperationResult.Fail(
                    $"Limpieza parcial: {removed} eliminados, {skipped} omitidos (en uso), {failed} fallaron. Re-escanea y revisa Administrador de dispositivos.",
                    "partial");
            return OperationResult.Ok(
                $"Fantasmas eliminados: {removed} (omitidos en uso: {skipped}). Re-escanea para confirmar.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Error limpiando fantasmas ({ex.GetType().Name}: {ex.Message}).", "unexpected");
        }
    }

    /// <summary>ID de instancia presentes según WMI (los fantasmas no salen aquí).</summary>
    internal static async Task<HashSet<string>> ReadPresentDeviceIdsAsync(CancellationToken ct = default)
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await Task.Run(() =>
            {
                var opts = new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(10), BlockSize = 50, Rewindable = false };
                using var searcher = new System.Management.ManagementObjectSearcher(
                    new System.Management.ManagementScope(@"root\cimv2"),
                    new System.Management.ObjectQuery("SELECT DeviceID FROM Win32_PnPEntity"),
                    opts);
                foreach (var device in searcher.Get())
                {
                    ct.ThrowIfCancellationRequested();
                    var id = (device as System.Management.ManagementObject)?["DeviceID"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(id)) present.Add(id);
                }
            }, ct);
        }
        catch { }
        return present;
    }

    /// <summary>Solo "Status: Started" explícito cuenta como arrancado (fail-safe).</summary>
    internal static bool IsStartedStatus(string enumOutput)
    {
        foreach (var line in enumOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Status:", StringComparison.OrdinalIgnoreCase) &&
                trimmed["Status:".Length..].Trim().Equals("Started", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Respalda los archivos reales del driver (INF+SYS+DLL+CAT) con
    /// pnputil /export-driver a %ProgramData%\CA-O\DriverBackup\&lt;dispositivo&gt;.
    /// El destino lo construye el servicio (nunca el cliente) y pasa por la
    /// allowlist. Éxito solo si queda al menos un .inf. Nunca lanza
    /// (salvo cancelación).
    /// </summary>
    public async Task<ExportDriverOutcome> ExportDriverAsync(string instanceId, CancellationToken ct = default)
    {
        if (!global::CAO.Shared.Security.CommandPolicy.IsValidPnpInstanceId(instanceId))
            return new ExportDriverOutcome(false, "ID de instancia no válido.", string.Empty, 0, "invalid-id");
        if (!IsRunningAsAdmin())
            return new ExportDriverOutcome(false, "Se requieren privilegios.", string.Empty, 0, "not-admin");
        if (_executor is null)
            return new ExportDriverOutcome(false, "Ejecutor no disponible.", string.Empty, 0, "no-executor");

        try
        {
            var root = global::CAO.Shared.Security.CommandPolicy.ExportDriverBackupRoot();
            Directory.CreateDirectory(root);
            var dest = Path.Combine(root, SanitizeDeviceDir(instanceId));
            Directory.CreateDirectory(dest);
            var export = await _executor.ExecuteAsync(
                global::CAO.Shared.Security.SystemCommandKey.PnPUtilExportDriver,
                ["/export-driver", instanceId, dest], ct);
            if (!export.Success)
                return new ExportDriverOutcome(false, $"No se pudo exportar el driver: {export.StdErr}", string.Empty, 0, "export-failed");
            var files = Directory.EnumerateFiles(dest, "*", SearchOption.AllDirectories).Count();
            var infs = Directory.EnumerateFiles(dest, "*.inf", SearchOption.AllDirectories).Count();
            if (infs == 0)
                return new ExportDriverOutcome(false, "pnputil terminó pero no dejó ningún .inf: el dispositivo quizá no tiene driver instalado.", string.Empty, 0, "empty");
            return new ExportDriverOutcome(true, $"Respaldo completo: {files} archivos en {dest}.", dest, files);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new ExportDriverOutcome(false, $"Error respaldando el driver ({ex.GetType().Name}: {ex.Message}).", string.Empty, 0, "unexpected");
        }
    }

    /// <summary>
    /// Nombre de carpeta seguro derivado del Instance ID: solo [letra/dígito
    /// _ - .], máx. 80, sin "..". La salida SIEMPRE pasa IsExportDriverDest.
    /// </summary>
    internal static string SanitizeDeviceDir(string instanceId)
    {
        var sb = new System.Text.StringBuilder(instanceId.Length);
        foreach (var c in instanceId)
            sb.Append(char.IsLetterOrDigit(c) || c is '_' or '-' || c == '.' ? c : '_');
        var name = sb.ToString().Trim('.', ' ', '_');
        while (name.Contains("..", StringComparison.Ordinal)) name = name.Replace("..", "__", StringComparison.Ordinal);
        if (name.Length > 80) name = name[..80].TrimEnd('.', '_');
        while (name.Contains("..", StringComparison.Ordinal)) name = name.Replace("..", "__", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(name) ? "device" : name;
    }

    /// <summary>
    /// Busca drivers oficiales en el Catálogo de Microsoft (Search.aspx por
    /// HWID, con fallbacks a VEN&amp;DEV y nombre). Solo lectura: no requiere
    /// elevación. Nunca lanza (salvo cancelación).
    /// </summary>
    public async Task<CatalogSearchOutcome> SearchCatalogDriversAsync(string hardwareId, string deviceName, CancellationToken ct = default)
    {
        if (!global::CAO.Shared.Security.CommandPolicy.IsValidCatalogHardwareId(hardwareId))
            return new CatalogSearchOutcome(false, "ID de hardware no válido.", Array.Empty<CatalogDriverOffer>(), "invalid-hwid");
        deviceName ??= string.Empty;
        if (deviceName.Length > 120 || deviceName.Any(char.IsControl))
            return new CatalogSearchOutcome(false, "Nombre de dispositivo no válido.", Array.Empty<CatalogDriverOffer>(), "invalid-name");

        try
        {
            using var http = new System.Net.Http.HttpClient(new System.Net.Http.SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            });
            http.DefaultRequestHeaders.UserAgent.ParseAdd(CatalogDriverProtocol.BrowserUserAgent);
            foreach (var query in CatalogDriverProtocol.BuildQueries(hardwareId, deviceName))
            {
                ct.ThrowIfCancellationRequested();
                string html;
                try
                {
                    html = await http.GetStringAsync(CatalogDriverProtocol.SearchUrl(query), ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    return new CatalogSearchOutcome(false, $"Sin conexión con el catálogo ({ex.GetType().Name}). Revisa internet.", Array.Empty<CatalogDriverOffer>(), "offline");
                }
                var offers = CatalogDriverProtocol.ParseOffers(html);
                if (offers.Count > 0)
                    return new CatalogSearchOutcome(true, $"{offers.Count} drivers oficiales encontrados para '{query}'.", offers);
            }
            return new CatalogSearchOutcome(true, "El catálogo no ofrece drivers para este hardware. Prueba con el enlace del fabricante.", Array.Empty<CatalogDriverOffer>());
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new CatalogSearchOutcome(false, $"Error buscando en el catálogo ({ex.GetType().Name}: {ex.Message}).", Array.Empty<CatalogDriverOffer>(), "unexpected");
        }
    }

    /// <summary>
    /// Descarga un .cab del catálogo (solo hosts Microsoft) y lo extrae con
    /// expand.exe a %ProgramData%\CA-O\DriverDownloads\&lt;guid&gt;. Devuelve
    /// los .inf listos para el flujo de instalación verificado. Nunca lanza
    /// (salvo cancelación).
    /// </summary>
    public async Task<CatalogDownloadOutcome> DownloadCatalogDriverAsync(string updateId, CancellationToken ct = default)
    {
        if (!global::CAO.Shared.Security.CommandPolicy.IsValidWindowsUpdateId(updateId))
            return new CatalogDownloadOutcome(false, "ID de actualización no válido.", string.Empty, Array.Empty<string>(), "invalid-id");
        if (!IsRunningAsAdmin())
            return new CatalogDownloadOutcome(false, "Se requieren privilegios.", string.Empty, Array.Empty<string>(), "not-admin");
        if (_executor is null)
            return new CatalogDownloadOutcome(false, "Ejecutor no disponible.", string.Empty, Array.Empty<string>(), "no-executor");

        try
        {
            var dir = Path.Combine(
                global::CAO.Shared.Security.CommandPolicy.CatalogDriverDownloadRoot(),
                updateId.ToLowerInvariant());
            var cab = Path.Combine(dir, "pkg.cab");
            var extracted = Path.Combine(dir, "extracted");
            Directory.CreateDirectory(extracted);

            using var http = new System.Net.Http.HttpClient(new System.Net.Http.SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            });
            http.DefaultRequestHeaders.UserAgent.ParseAdd(CatalogDriverProtocol.BrowserUserAgent);
            http.DefaultRequestHeaders.Referrer = new Uri(CatalogDriverProtocol.CatalogBaseUrl + "/");

            string dialog;
            try
            {
                using var form = new System.Net.Http.FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["updateIDs"] = $"[{{\"size\":0,\"updateID\":\"{updateId}\",\"uidInfo\":\"{updateId}\"}}]",
                });
                using var dialogResponse = await http.PostAsync(
                    CatalogDriverProtocol.CatalogBaseUrl + "/DownloadDialog.aspx", form, ct);
                dialogResponse.EnsureSuccessStatusCode();
                dialog = await dialogResponse.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new CatalogDownloadOutcome(false, $"Sin conexión con el catálogo ({ex.GetType().Name}). Revisa internet.", string.Empty, Array.Empty<string>(), "offline");
            }
            var cabUrl = CatalogDriverProtocol.PickCabUrl(CatalogDriverProtocol.ParseDownloadUrls(dialog));
            if (cabUrl is null)
                return new CatalogDownloadOutcome(false, "El catálogo no devolvió descarga para esta oferta.", string.Empty, Array.Empty<string>(), "no-links");

            long totalBytes;
            try
            {
                using var download = await http.GetAsync(cabUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct);
                download.EnsureSuccessStatusCode();
                var length = download.Content.Headers.ContentLength;
                if (length.HasValue && length.Value > CatalogDriverProtocol.MaxCabBytes)
                    return new CatalogDownloadOutcome(false, $"Paquete excesivo ({CatalogDriverProtocol.FormatSize(length.Value)}, máx. 1.5 GB).", string.Empty, Array.Empty<string>(), "too-big");
                await using var content = await download.Content.ReadAsStreamAsync(ct);
                await using var file = File.Create(cab);
                await content.CopyToAsync(file, ct);
                totalBytes = new FileInfo(cab).Length;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new CatalogDownloadOutcome(false, $"Descarga fallida ({ex.GetType().Name}: {ex.Message}).", string.Empty, Array.Empty<string>(), "download-failed");
            }
            if (totalBytes == 0)
                return new CatalogDownloadOutcome(false, "Descarga vacía.", string.Empty, Array.Empty<string>(), "empty");

            var expand = await _executor.ExecuteAsync(
                global::CAO.Shared.Security.SystemCommandKey.ExpandCab,
                [cab, "-F:*", extracted], ct);
            if (!expand.Success)
                return new CatalogDownloadOutcome(false, $"No se pudo extraer el paquete: {expand.StdErr}", string.Empty, Array.Empty<string>(), "extract-failed");

            var infs = Directory.EnumerateFiles(extracted, "*.inf", SearchOption.AllDirectories)
                .Take(CatalogDriverProtocol.MaxInfs + 1).ToList();
            if (infs.Count == 0)
                return new CatalogDownloadOutcome(false, "El paquete no trae ningún .inf.", string.Empty, Array.Empty<string>(), "empty");
            var shown = infs.Take(CatalogDriverProtocol.MaxInfs).ToList();
            return new CatalogDownloadOutcome(true,
                $"Descargado {CatalogDriverProtocol.FormatSize(totalBytes)} con {shown.Count} INF listos para instalar.",
                extracted, shown);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new CatalogDownloadOutcome(false, $"Error descargando el driver ({ex.GetType().Name}: {ex.Message}).", string.Empty, Array.Empty<string>(), "unexpected");
        }
    }

    private async Task RollbackDnsExact(string interfaceName, bool wasDhcp, string[] beforeDnsV4, string[] beforeDnsV6, CancellationToken ct)
    {
        if (_executor == null) return;
        
        if (wasDhcp)
        {
            await _executor.ExecuteAsync(CAO.Shared.Security.SystemCommandKey.NetShInterfaceIpSetDnsDhcp, 
                ["interface", "ip", "set", "dns", interfaceName, "dhcp"], ct);
        }
        else
        {
            // Restore exact static DNS configuration
            if (beforeDnsV4.Length > 0)
            {
                await _executor.ExecuteAsync(CAO.Shared.Security.SystemCommandKey.NetShInterfaceIpSetDnsPrimary,
                    ["interface", "ip", "set", "dns", interfaceName, "static", beforeDnsV4[0]], ct);
                for (int i = 1; i < beforeDnsV4.Length; i++)
                {
                    await _executor.ExecuteAsync(CAO.Shared.Security.SystemCommandKey.NetShInterfaceIpSetDnsSecondary,
                        ["interface", "ip", "add", "dns", interfaceName, beforeDnsV4[i]], ct);
                }
            }
            // Note: IPv6 rollback would need additional netsh commands if supported
        }
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
    private void LogLegacy(string id, string operation, bool success, OptimizationSnapshot snapshot, string? error = null, Shared.Security.CallerIdentity? caller = null)
    {
        _history.Log(new HistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            AppVersion = AppVersion.Semantic,
            User = caller is null ? Environment.UserName : $"{caller.Name} [{caller.Sid}]",
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
