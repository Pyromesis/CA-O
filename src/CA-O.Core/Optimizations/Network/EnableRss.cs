using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class EnableRss : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\enable-rss", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-rss",
        NameEs = "Habilitar Receive Side Scaling",
        NameEn = "Habilitar Receive Side Scaling",
        DescriptionEs = "Si NIC soporta y desactivado, Recommended/Conditional. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Si NIC soporta y desactivado, Recommended/Conditional.",
        TooltipEs = "enable-rss via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Network,
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
        return Task.FromResult(OperationResult.Ok("Habilitar Receive Side Scaling aplicado."));
    }
}
