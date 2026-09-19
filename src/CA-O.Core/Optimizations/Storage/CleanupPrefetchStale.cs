using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes stale prefetch traces (*.pf older than 30 days). Space
/// maintenance only: Windows rebuilds prefetch, so this never speeds anything up.</summary>
public sealed class CleanupPrefetchStale : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; } =
    [
        (@"%SystemRoot%\Prefetch", "*.pf", 30),
    ];

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-prefetch-stale",
        NameEs = "Limpiar prefetch antiguo",
        NameEn = "Clean stale prefetch",
        DescriptionEs = "Borra rastros prefetch (*.pf) de más de 30 días. Solo libera espacio: no acelera nada.",
        DescriptionEn = "Deletes prefetch traces (*.pf) older than 30 days. Frees space only: never speeds anything up.",
        TooltipEs = "Solo ficheros *.pf con más de 30 días. Windows lo reconstruye solo. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
