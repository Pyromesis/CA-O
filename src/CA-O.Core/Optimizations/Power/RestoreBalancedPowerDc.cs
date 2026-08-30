using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

public sealed class RestoreBalancedPowerDc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-balanced-power-dc", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-balanced-power-dc",
        NameEs = "Restaurar Balanced en DC",
        NameEn = "Restaurar Balanced en DC",
        DescriptionEs = "DC -> Balanced tras Gaming para evitar temperatura/consumo. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "DC -> Balanced tras Gaming para evitar temperatura/consumo.",
        TooltipEs = "restore-balanced-power-dc via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Restaurar Balanced en DC aplicado."));
    }
}
