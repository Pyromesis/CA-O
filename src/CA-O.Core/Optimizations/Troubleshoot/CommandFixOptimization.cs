using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>
/// Base para solucionadores que ejecutan comandos oficiales de Windows de la
/// lista blanca (CommandPolicy), en orden. No cambian configuración: no hay
/// nada que revertir. Verify solo pasa con evidencia de esta sesión.
/// </summary>
public abstract class CommandFixOptimization : IOptimization
{
    private int? _lastExitCode;

    public abstract OptimizationDefinition Definition { get; }

    /// <summary>Pasos a ejecutar. <c>Optional</c>: un fallo no corta la secuencia.</summary>
    protected abstract IReadOnlyList<(SystemCommandKey Key, string[] Arguments, bool Optional)> Steps { get; }

    protected abstract string SuccessMessage { get; }

    protected abstract string FailureMessage { get; }

    /// <summary>Códigos de salida que cuentan como éxito (por defecto solo 0).</summary>
    protected virtual bool IsSuccess(PrivilegedCommandResult result) => result.Success;

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        foreach (var (key, arguments, optional) in Steps)
        {
            ct.ThrowIfCancellationRequested();
            var result = await context.Executor.ExecuteAsync(key, arguments, ct);
            _lastExitCode = result.ExitCode;
            if (IsSuccess(result) || optional) continue;

            var detail = string.Join(" ",
                new[] { $"exit={result.ExitCode}", result.TimedOut ? "tiempo agotado" : null, result.StdErr }
                    .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            return OperationResult.Fail($"{FailureMessage} Detalle: {detail}", "command-failed");
        }

        _lastExitCode = 0;
        return OperationResult.Ok(SuccessMessage);
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("Esta solución no cambia configuración: no hay nada que revertir."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default) =>
        Task.FromResult(_lastExitCode switch
        {
            null => VerificationResult.Unknown(OptimizationState.Unknown, "Sin evidencia de ejecución en esta sesión."),
            0 => VerificationResult.Passed(OptimizationState.AppliedByCao, "Comandos completados sin error."),
            var code => VerificationResult.Failed(OptimizationState.NotApplied, $"Un comando terminó con exit={code}."),
        });

    protected static OptimizationDefinition FixDefinition(
        string id, string nameEs, string nameEn, string descEs, string descEn, string tipEs,
        OptimizationCategory category, RiskLevel risk = RiskLevel.Low) => new()
    {
        Id = id,
        NameEs = nameEs,
        NameEn = nameEn,
        DescriptionEs = descEs,
        DescriptionEn = descEn,
        TooltipEs = tipEs,
        Category = category,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = risk,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible,
    };
}

/// <summary>Sin internet o "red sin acceso": renueva la concesión DHCP de todos los adaptadores.</summary>
public sealed class RenewIpAddress : CommandFixOptimization
{
    public override OptimizationDefinition Definition => FixDefinition(
        "renew-ip-address", "Renovar dirección IP", "Renew IP address",
        "Pide al router una dirección IP nueva. Arregla 'sin internet' o 'red sin acceso' tras cambiar de router.",
        "Asks the router for a new IP address. Fixes 'no internet' after changing routers.",
        "ipconfig /release y /renew en todos los adaptadores. La red se corta unos segundos. Sin cambios de configuración.",
        OptimizationCategory.Network);

    // release falla sin DHCP (IP fija): no debe impedir el renew.
    protected override IReadOnlyList<(SystemCommandKey Key, string[] Arguments, bool Optional)> Steps { get; } =
    [
        (SystemCommandKey.IpConfigRelease, ["/release"], true),
        (SystemCommandKey.IpConfigRenew, ["/renew"], false),
        (SystemCommandKey.IpConfigFlushDns, ["/flushdns"], true),
    ];

    protected override string SuccessMessage => "Dirección IP renovada y caché DNS vaciada.";
    protected override string FailureMessage => "No se pudo renovar la IP (¿IP fija o router sin DHCP?).";
}

/// <summary>Conexión local rara tras cambiar de router o de red: vacía la caché ARP.</summary>
public sealed class ClearArpCache : CommandFixOptimization
{
    public override OptimizationDefinition Definition => FixDefinition(
        "clear-arp-cache", "Vaciar caché ARP", "Clear ARP cache",
        "Olvida las direcciones físicas de la red local. Arregla 'no llega al router' tras cambiar de equipo de red.",
        "Forgets local network physical addresses. Fixes 'cannot reach router' after swapping network gear.",
        "netsh interface ip delete arpcache. Inmediato y seguro; Windows la vuelve a llenar sola.",
        OptimizationCategory.Network);

    protected override IReadOnlyList<(SystemCommandKey Key, string[] Arguments, bool Optional)> Steps { get; } =
    [
        (SystemCommandKey.NetShDeleteArpCache, ["interface", "ip", "delete", "arpcache"], false),
    ];

    protected override string SuccessMessage => "Caché ARP vaciada.";
    protected override string FailureMessage => "No se pudo vaciar la caché ARP.";
}

/// <summary>Errores raros, apps que se cierran, menús que no abren: SFC repara ficheros del sistema.</summary>
public sealed class RunSystemFileChecker : CommandFixOptimization
{
    public override OptimizationDefinition Definition => FixDefinition(
        "run-system-file-checker", "Reparar archivos del sistema (SFC)", "Repair system files (SFC)",
        "Comprueba y repara ficheros de Windows dañados. Para errores raros, cierres o menús que no abren.",
        "Checks and repairs damaged Windows files. For odd errors, crashes or menus that do not open.",
        "Ejecuta sfc /scannow, la herramienta oficial de Microsoft. Tarda de 5 a 20 minutos; no apagues el equipo. El detalle queda en CBS.log.",
        OptimizationCategory.Performance);

    protected override IReadOnlyList<(SystemCommandKey Key, string[] Arguments, bool Optional)> Steps { get; } =
    [
        (SystemCommandKey.SfcScanNow, ["/scannow"], false),
    ];

    protected override string SuccessMessage => "Comprobación completada: ficheros del sistema correctos o reparados (detalle en Windows\\Logs\\CBS\\CBS.log).";
    protected override string FailureMessage => "SFC encontró ficheros que no pudo reparar: ejecuta 'Reparar imagen de Windows' y vuelve a probar.";
}

/// <summary>Cuando SFC no puede reparar: DISM restaura la imagen desde Windows Update.</summary>
public sealed class RepairWindowsImage : CommandFixOptimization
{
    public override OptimizationDefinition Definition => FixDefinition(
        "repair-windows-image", "Reparar imagen de Windows (DISM)", "Repair Windows image (DISM)",
        "Repara el almacén de componentes desde Windows Update. Úsalo cuando SFC no puede arreglar algo.",
        "Repairs the component store from Windows Update. Use it when SFC cannot fix something.",
        "Ejecuta DISM /Online /Cleanup-Image /RestoreHealth. Necesita internet y tarda de 10 a 30 minutos. Después, repite SFC.",
        OptimizationCategory.Performance);

    protected override IReadOnlyList<(SystemCommandKey Key, string[] Arguments, bool Optional)> Steps { get; } =
    [
        (SystemCommandKey.DismRestoreHealth, ["/Online", "/Cleanup-Image", "/RestoreHealth"], false),
    ];

    protected override string SuccessMessage => "Imagen de Windows reparada. Ahora ejecuta 'Reparar archivos del sistema'.";
    protected override string FailureMessage => "DISM no pudo reparar la imagen (¿sin internet?).";
}
