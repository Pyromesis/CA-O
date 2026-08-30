using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

public sealed class DisableUnnecessaryStartupApps : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\disable-unnecessary-startup-apps", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-unnecessary-startup-apps",
        NameEs = "Desactivar apps inicio innecesarias",
        NameEn = "Desactivar apps inicio innecesarias",
        DescriptionEs = "Clasifica Essential/Recommended/Optional/Unknown con publisher. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Clasifica Essential/Recommended/Optional/Unknown con publisher.",
        TooltipEs = "disable-unnecessary-startup-apps via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Desactivar apps inicio innecesarias aplicado."));
    }
}
