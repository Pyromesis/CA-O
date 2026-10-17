using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Informes de Windows Error Reporting (ReportArchive, ReportQueue y Temp del sistema y de cada usuario) con más de 7 días. Es la entrada equivalente del Liberador de espacio.</summary>
public sealed class CleanupWindowsErrorReports : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        foreach (var sub in new[] { "ReportArchive", "ReportQueue", "Temp" })
            list.Add((@"%ProgramData%\Microsoft\Windows\WER\" + sub, "*.*", 7 * MinutesPerDay));
        foreach (var dir in ProfileSubDirs("AppData", "Local", "Microsoft", "Windows", "WER"))
            list.Add((dir, "*.*", 7 * MinutesPerDay));
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-windows-error-reports",
        NameEs = "Limpiar informes de errores de Windows",
        NameEn = "Clean Windows error reports",
        DescriptionEs = "Borra informes de errores (WER) archivados y en cola de más de 7 días.",
        DescriptionEn = "Deletes archived and queued Windows error reports (WER) older than 7 days.",
        TooltipEs = "La misma opción 'Informes de errores de Windows' del Liberador de espacio. Conserva la última semana. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
