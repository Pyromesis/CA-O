using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class OptimizeHddMediaAware : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\optimize-hdd-media-aware", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "optimize-hdd-media-aware",
        NameEs = "Optimizar HDD media-aware",
        NameEn = "Optimizar HDD media-aware",
        DescriptionEs = "Usa defrag /O segun medio, no ReTrim en HDD. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Usa defrag /O segun medio, no ReTrim en HDD.",
        TooltipEs = "optimize-hdd-media-aware via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
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
        return Task.FromResult(OperationResult.Ok("Optimizar HDD media-aware aplicado."));
    }
}
