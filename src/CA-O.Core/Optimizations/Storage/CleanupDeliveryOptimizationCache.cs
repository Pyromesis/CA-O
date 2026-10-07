using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes cached Delivery Optimization payload files.</summary>
public sealed class CleanupDeliveryOptimizationCache : TempFileCleanupOptimization
{
    // CAO-BUG-2026-10-06: OlderThanDays = 0 fijaba el corte en "ahora", asi que el
    // filtro age <= cutoff admitia TAMBIEN los chunks que el servicio Delivery
    // Optimization estaba descargando en ese instante. A diferencia de
    // CleanupWindowsUpdateCache, esta limpieza no detiene el servicio antes de
    // borrar, de modo que podia eliminar un chunk a medio escribir. Se usa 1 dia,
    // el mismo margen que el resto de la familia de temporales: el contenido en
    // transito se esta reescribiendo y por tanto queda fuera del corte.
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; } =
    [
        (@"%SystemRoot%\SoftwareDistribution\DeliveryOptimization\Cache", "*.*", 1),
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
