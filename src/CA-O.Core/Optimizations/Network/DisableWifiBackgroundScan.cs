using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Parsea `netsh wlan show autoconfig`: devuelve true si la lógica de
    /// autoconfiguración está habilitada en `iface`, false si está
    /// deshabilitada, null si la salida no trae esa interfaz. Verificado en
    /// máquina real: `netsh wlan show /?` lista `show autoconfig` ("shows
    /// whether the auto configuration logic is enabled or disabled") mientras
    /// que `show settings` solo trae ajustes globales (sin autoconfig), por
    /// eso no se usa. Sin interfaz en el equipo la salida viene vacía.
    /// </summary>
    internal static bool? ParseAutoconfigState(string output, string iface)
    {
        if (string.IsNullOrEmpty(output) || string.IsNullOrWhiteSpace(iface))
            return null;
        foreach (Match match in Regex.Matches(output,
            "Auto\\s+configuration\\s+logic\\s+is\\s+(enabled|disabled)\\s+on\\s+interface\\s+\"?([^\"\\r\\n]+)\"?",
            RegexOptions.IgnoreCase))
        {
            if (string.Equals(match.Groups[2].Value.Trim().Trim('"'), iface.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups[1].Value.Equals("enabled", StringComparison.OrdinalIgnoreCase);
            }
        }
        return null;
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        // Best-effort read-only (`show autoconfig`, ruta fija, timeout 5s).
        // Sin wifi o sin parse fiable → Unknown con motivo (como antes);
        // RULING: Unknown o real, nunca NotApplied ciego. Nunca lanza.
        try
        {
            var iface = FindLiveWifiInterface();
            if (string.IsNullOrWhiteSpace(iface))
                return OptimizationState.Unknown;
            var netsh = Path.Combine(
                Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32"), "netsh.exe");
            using var process = new Process();
            process.StartInfo.FileName = netsh;
            process.StartInfo.ArgumentList.Add("wlan");
            process.StartInfo.ArgumentList.Add("show");
            process.StartInfo.ArgumentList.Add("autoconfig");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            // Anti-deadlock BCL-only: drenar stdout+stderr en concurrente con
            // ReadToEndAsync y acotar con Task.Delay (nunca bloquear en un
            // ReadToEnd sincrono con el otro pipe sin drenar). Detect sigue
            // sync por firma: el timeout lo pone el Delay, no el WaitForExit.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var readAll = Task.WhenAll(stdoutTask, stderrTask);
            if (Task.WhenAny(readAll, Task.Delay(5000)).GetAwaiter().GetResult() != readAll)
            {
                try { process.Kill(); } catch { }
                return OptimizationState.Unknown;
            }
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                return OptimizationState.Unknown;
            }
            var stdout = stdoutTask.GetAwaiter().GetResult();
            _ = stderrTask.GetAwaiter().GetResult();
            var enabled = ParseAutoconfigState(stdout, iface);
            return enabled switch
            {
                false => OptimizationState.AppliedByCao,
                true => OptimizationState.NotApplied,
                _ => OptimizationState.Unknown,
            };
        }
        catch
        {
            return OptimizationState.Unknown;
        }
    }

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
