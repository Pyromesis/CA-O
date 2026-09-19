using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes CBS servicing logs (*.log older than 30 days). Space
/// maintenance only: Windows regenerates them, so this never speeds anything up.</summary>
public sealed class CleanupCbsLogs : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; } =
    [
        (@"%SystemRoot%\Logs\CBS", "*.log", 30),
    ];

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-cbs-logs",
        NameEs = "Limpiar registros CBS",
        NameEn = "Clean CBS logs",
        DescriptionEs = "Borra registros CBS (*.log) de más de 30 días. Solo libera espacio.",
        DescriptionEn = "Deletes CBS logs (*.log) older than 30 days. Frees space only.",
        TooltipEs = @"Solo *.log con más de 30 días en Windows\Logs\CBS. Mantenimiento no reversible.",
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
