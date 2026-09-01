using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>
/// Sets Windows to prioritize High Performance mode settings on AC power.
/// Modifies HKCU registry to disable power savings on AC.
/// Official Windows registry configuration (documented in MS-Windows-Power-Control).
/// </summary>
public sealed class SetBestPerformanceAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Disable hard disk timeout on AC (keeps disk active)
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Control Panel\PowerCfg\PowerPolicies\0",
                "ConservationIdleTimeout",
                0, // 0 = Never timeout
                RegistryValueKind2.DWord),
            // Disable monitor timeout on AC
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Control Panel\PowerCfg\PowerPolicies\0",
                "VideoTimeout",
                0, // 0 = Never timeout
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "set-best-performance-ac",
        NameEs = "Modo Alto Rendimiento en AC",
        NameEn = "High Performance mode on AC power",
        DescriptionEs = "Deshabilita suspensión de disco y pantalla en AC para máximo rendimiento. Solo aplica cuando está conectado.",
        DescriptionEn = "Disables disk and monitor suspension on AC for maximum performance. Only applies when plugged in.",
        TooltipEs = "Modifica HKCU\\Control Panel\\PowerCfg\\PowerPolicies. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Modo Alto Rendimiento configurado en AC."));
    }
}
