using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Performance;

/// <summary>
/// #1: Disable VBS/Hypervisor via bcdedit. CRITICAL SECURITY TRADE-OFF:
/// Expert-only, double-confirmed, reboot required. The previous
/// hypervisorlaunchtype value is captured and restored on revert.
/// </summary>
public sealed class DisableVbs : IOptimization
{
    private const string UltimateNote = "bcdedit /set {current} hypervisorlaunchtype off";

    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar VBS / Hipervisor (CRÍTICO)",
        NameEn = "Disable VBS / Hypervisor (CRITICAL)",
        DescriptionEs = "Apaga Virtualization-Based Security. Reduce la seguridad del kernel; solo para expertos.",
        DescriptionEn = "Turns off Virtualization-Based Security. Reduces kernel security; experts only.",
        TooltipEs = "REDUCCIÓN DE SEGURIDAD: pierdes HVCI/aislamiento de kernel y puede afectar a Vanguard/anti-cheats que exijan seguridad activa, además de romper WSL2/Docker/Sandbox. Requiere reinicio.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.RequiredSecurityFeature,
        Risk = RiskLevel.Critical,
        Compatibility = CompatibilityStatus.PotentialConflict,
        SecurityImpact = SecurityImpact.ReducedProtection,
        Impact = ImpactLevel.High,
        Flags = OptimizationFlags.ExpertOnly | OptimizationFlags.SecurityTradeoff | OptimizationFlags.RequiresReboot,
    };

    private static string Id => "disable-vbs";

    public OptimizationState Detect(IRegistryAccessor registry) => CurrentLaunchType switch
    {
        null => OptimizationState.Unknown,
        "Off" => OptimizationState.AppliedByCao,
        _ => OptimizationState.NotApplied,
    };

    /// <summary>Parsed from `bcdedit /enum {current}`; injected by the engine.</summary>
    public string? CurrentLaunchType { get; set; }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"hypervisorlaunchtype={CurrentLaunchType ?? "Auto"}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
        {
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");
        }

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.BcdEditHypervisorOff,
            ["/set", "{current}", "hypervisorlaunchtype", "off"], ct);
        return result.Success
            ? OperationResult.Ok("VBS desactivado. REINICIA para que aplique.")
            : OperationResult.Fail("No se pudo modificar el BCD (¿permisos de administrador?).", result.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
        {
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");
        }
        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("hypervisorlaunchtype=", StringComparison.Ordinal));
        var previous = note?["hypervisorlaunchtype=".Length..] ?? "Auto";
        // A captured Off means the hypervisor was already off: restoring to
        // Auto is the safe canonical state (policy pins restore to Auto).
        if (string.Equals(previous, "off", StringComparison.OrdinalIgnoreCase)) previous = "Auto";
        if (!string.Equals(previous, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Fail("Estado previo del hypervisor no restaurable por política.", "CAO-SEC-011");
        }

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.BcdEditHypervisorRestore,
            ["/set", "{current}", "hypervisorlaunchtype", "Auto"], ct);
        return result.Success
            ? OperationResult.Ok("Hypervisor restaurado a 'Auto'. Reinicia.")
            : OperationResult.Fail("No se pudo restaurar el hypervisor.", result.StdErr);
    }
}
