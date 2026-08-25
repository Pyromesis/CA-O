using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;

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
        if (context.Process is null) return OperationResult.Fail("Runner no disponible.", "IProcessRunner null");
        var (code, output) = await context.Process.RunAsync("bcdedit", "/set {current} hypervisorlaunchtype off", ct);
        return code == 0
            ? OperationResult.Ok("VBS desactivado. REINICIA para que aplique.")
            : OperationResult.Fail("No se pudo modificar el BCD (¿permisos de administrador?).", output);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Process is null) return OperationResult.Fail("Runner no disponible.", "IProcessRunner null");
        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("hypervisorlaunchtype=", StringComparison.Ordinal));
        var previous = note?["hypervisorlaunchtype=".Length..] ?? "Auto";
        if (string.Equals(previous, "Off", StringComparison.OrdinalIgnoreCase)) previous = "Auto";

        var (code, output) = await context.Process.RunAsync("bcdedit", $"/set {{current}} hypervisorlaunchtype {previous}", ct);
        return code == 0
            ? OperationResult.Ok($"Hypervisor restaurado a '{previous}'. Reinicia.")
            : OperationResult.Fail("No se pudo restaurar el hypervisor.", output);
    }
}
