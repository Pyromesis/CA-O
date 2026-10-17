using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Registros (logs) e informes de fallo (crashes) de OBS Studio con más de 7 días. OBS escribe uno por sesión y se acumulan; escenas, perfiles y grabaciones no se tocan.</summary>
public sealed class CleanupObsLogs : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        foreach (var sub in new[] { "logs", "crashes" })
        {
            foreach (var dir in ProfileSubDirs("AppData", "Roaming", "obs-studio", sub))
                list.Add((dir, "*.*", 7 * MinutesPerDay));
        }
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-obs-logs",
        NameEs = "Limpiar registros y volcados de OBS Studio",
        NameEn = "Clean OBS Studio logs and crash dumps",
        DescriptionEs = "Borra registros e informes de fallo de OBS Studio de más de 7 días.",
        DescriptionEn = "Deletes OBS Studio logs and crash reports older than 7 days.",
        TooltipEs = "Solo obs-studio\\logs y obs-studio\\crashes, con más de 7 días. Nunca escenas, perfiles ni grabaciones. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
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
