using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

public sealed class RestorePowerPlanAfterGaming : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-power-plan-after-gaming", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-power-plan-after-gaming",
        NameEs = "Restaurar plan tras gaming",
        NameEn = "Restaurar plan tras gaming",
        DescriptionEs = "Guarda modo previo y restaura Gaming->Best Performance. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Guarda modo previo y restaura Gaming->Best Performance.",
        TooltipEs = "restore-power-plan-after-gaming via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Restaurar plan tras gaming aplicado."));
    }
}
