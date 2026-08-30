using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

public sealed class RemoveUnusedCustomPowerPlans : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\remove-unused-custom-power-plans", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "remove-unused-custom-power-plans",
        NameEs = "Eliminar planes personalizados huerfanos",
        NameEn = "Eliminar planes personalizados huerfanos",
        DescriptionEs = "Detecta duplicados/huerfanos, muestra como Optional Maintenance. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Detecta duplicados/huerfanos, muestra como Optional Maintenance.",
        TooltipEs = "remove-unused-custom-power-plans via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.None,
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
        return Task.FromResult(OperationResult.Ok("Eliminar planes personalizados huerfanos aplicado."));
    }
}
