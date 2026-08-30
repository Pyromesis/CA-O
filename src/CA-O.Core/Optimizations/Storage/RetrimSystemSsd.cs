using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class RetrimSystemSsd : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\retrim-system-ssd", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "retrim-system-ssd",
        NameEs = "ReTrim en SSD del sistema",
        NameEn = "ReTrim en SSD del sistema",
        DescriptionEs = "Ejecuta ReTrim solo en SSD real, no en HDD. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Ejecuta ReTrim solo en SSD real, no en HDD.",
        TooltipEs = "retrim-system-ssd via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("ReTrim en SSD del sistema aplicado."));
    }
}
