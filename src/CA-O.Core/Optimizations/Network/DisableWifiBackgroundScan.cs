using System.Net.NetworkInformation;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// Desactiva el escaneo Wi-Fi en segundo plano (netsh wlan autoconfig
/// enabled=no): los barridos periódicos causan picos de ping en juego.
/// OJO: para conectarse a redes NUEVAS hay que reactivarlo (la UI lo avisa).
/// Solo Expertos. Reversible (enabled=yes).
/// </summary>
public sealed class DisableWifiBackgroundScan : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "disable-wifi-background-scan",
        NameEs = "Wi-Fi: sin escaneos en segundo plano",
        NameEn = "Wi-Fi: no background scans",
        DescriptionEs = "Frena los barridos Wi-Fi periódicos que provocan picos de ping (netsh autoconfig off).",
        DescriptionEn = "Stops periodic Wi-Fi background scans that cause ping spikes (netsh autoconfig off).",
        TooltipEs = "Mientras esté off, para unirte a una red NUEVA reactívalo (aquí mismo, Revertir). No afecta a tu red actual. Solo Expertos.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.ExpertOnly,
    };

    /// <summary>Elige la Wi-Fi activa (o la primera presente). Puro y testeable.</summary>
    internal static string? FindWifiInterface(IEnumerable<(string Name, NetworkInterfaceType Type, OperationalStatus Status)> nics) =>
        nics.Where(n => n.Type == NetworkInterfaceType.Wireless80211)
            .OrderByDescending(n => n.Status == OperationalStatus.Up)
            .Select(n => n.Name)
            .FirstOrDefault();

    private static string? FindLiveWifiInterface()
    {
        try
        {
            return FindWifiInterface(NetworkInterface.GetAllNetworkInterfaces()
                .Select(n => (n.Name, n.NetworkInterfaceType, n.OperationalStatus)));
        }
        catch { return null; }
    }

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"wifi-iface={FindLiveWifiInterface() ?? string.Empty}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");
        var iface = FindLiveWifiInterface();
        if (string.IsNullOrWhiteSpace(iface))
            return OperationResult.Fail("Sin adaptador Wi-Fi en este equipo.", "no-wifi");
        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.NetShWlanAutoconfig,
            ["wlan", "set", "autoconfig", "enabled=no", $"interface=\"{iface}\""], ct);
        return result.Success
            ? OperationResult.Ok($"Escaneos Wi-Fi detenidos en '{iface}'. Para redes nuevas, revierte primero.")
            : OperationResult.Fail("No se pudo detener el escaneo Wi-Fi.", result.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");
        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("wifi-iface=", StringComparison.Ordinal));
        var iface = note?["wifi-iface=".Length..]?.Trim() ?? FindLiveWifiInterface();
        if (string.IsNullOrWhiteSpace(iface))
            return OperationResult.Fail("Sin registro de interfaz Wi-Fi.", "no-iface");
        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.NetShWlanAutoconfig,
            ["wlan", "set", "autoconfig", "enabled=yes", $"interface=\"{iface}\""], ct);
        return result.Success
            ? OperationResult.Ok($"Escaneo Wi-Fi reactivado en '{iface}'.")
            : OperationResult.Fail("No se pudo reactivar el escaneo Wi-Fi.", result.StdErr);
    }
}
