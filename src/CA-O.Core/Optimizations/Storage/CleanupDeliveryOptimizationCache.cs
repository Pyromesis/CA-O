using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes cached Delivery Optimization payload files.</summary>
public sealed class CleanupDeliveryOptimizationCache : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; } =
    [
        (@"%SystemRoot%\SoftwareDistribution\DeliveryOptimization\Cache", "*.*", 0),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-delivery-optimization-cache",
        NameEs = "Limpiar caché Delivery Optimization",
        NameEn = "Cleanup Delivery Optimization cache",
        DescriptionEs = "Borra los ficheros cacheados de Delivery Optimization para liberar espacio en disco.",
        DescriptionEn = "Deletes cached Delivery Optimization payloads to free disk space.",
        TooltipEs = "Vacía la caché DO del sistema. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
