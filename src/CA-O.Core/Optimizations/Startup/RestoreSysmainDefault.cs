using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

public sealed class RestoreSysmainDefault : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-sysmain-default", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-sysmain-default",
        NameEs = "Restaurar SysMain por defecto",
        NameEn = "Restaurar SysMain por defecto",
        DescriptionEs = "Corrige si tweak externo deshabilito SysMain. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Corrige si tweak externo deshabilito SysMain.",
        TooltipEs = "restore-sysmain-default via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Restaurar SysMain por defecto aplicado."));
    }
}
