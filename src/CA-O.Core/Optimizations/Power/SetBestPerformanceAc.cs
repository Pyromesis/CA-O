using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

public sealed class SetBestPerformanceAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\set-best-performance-ac", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "set-best-performance-ac",
        NameEs = "Best Performance en AC",
        NameEn = "Best Performance en AC",
        DescriptionEs = "AC -> Best Performance solo Desktop/AC Gaming. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "AC -> Best Performance solo Desktop/AC Gaming.",
        TooltipEs = "set-best-performance-ac via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Best Performance en AC aplicado."));
    }
}
