using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

public sealed class RestoreDefaultGpuPreference : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-default-gpu-preference", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-default-gpu-preference",
        NameEs = "Restaurar preferencia GPU por defecto",
        NameEn = "Restaurar preferencia GPU por defecto",
        DescriptionEs = "Restaura preferencia absurda a SystemDefault. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Restaura preferencia absurda a SystemDefault.",
        TooltipEs = "restore-default-gpu-preference via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Tiny,
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
        return Task.FromResult(OperationResult.Ok("Restaurar preferencia GPU por defecto aplicado."));
    }
}
