using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class CleanupDeliveryOptimizationCache : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\cleanup-delivery-optimization-cache", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-delivery-optimization-cache",
        NameEs = "Limpiar cache Delivery Optimization",
        NameEn = "Limpiar cache Delivery Optimization",
        DescriptionEs = "Solo si tamano significativo y sin descarga activa. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Solo si tamano significativo y sin descarga activa.",
        TooltipEs = "cleanup-delivery-optimization-cache via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Limpiar cache Delivery Optimization aplicado."));
    }
}
