using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

public sealed class DisableHeavyStartupApps : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\disable-heavy-startup-apps", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-heavy-startup-apps",
        NameEs = "Desactivar apps inicio pesadas",
        NameEn = "Desactivar apps inicio pesadas",
        DescriptionEs = "Impacto High, requiere seleccion usuario, excluye AV/drivers. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Impacto High, requiere seleccion usuario, excluye AV/drivers.",
        TooltipEs = "disable-heavy-startup-apps via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Desactivar apps inicio pesadas aplicado."));
    }
}
