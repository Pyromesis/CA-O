using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

public sealed class SetWirelessAdapterMaxPerformanceAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\set-wireless-adapter-max-performance-ac", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "set-wireless-adapter-max-performance-ac",
        NameEs = "Wi-Fi max rendimiento en AC",
        NameEn = "Wi-Fi max rendimiento en AC",
        DescriptionEs = "Solo si Wi-Fi existe, gaming y AC. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Solo si Wi-Fi existe, gaming y AC.",
        TooltipEs = "set-wireless-adapter-max-performance-ac via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Wi-Fi max rendimiento en AC aplicado."));
    }
}
