using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Registros de los launchers (Riot Client, EA app, Epic, Ubisoft Connect) con más de 7 días. Los de la última semana se conservan para poder reportar un fallo.</summary>
public sealed class CleanupGameLauncherLogs : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        string[][] logDirs =
        [
            ["AppData", "Local", "Riot Games", "Riot Client", "Logs"],
            ["AppData", "Local", "Electronic Arts", "EA Desktop", "Logs"],
            ["AppData", "Local", "EpicGamesLauncher", "Saved", "Logs"],
            ["AppData", "Local", "Ubisoft Game Launcher", "logs"],
        ];
        foreach (var parts in logDirs)
        {
            foreach (var dir in ProfileSubDirs(parts))
                list.Add((dir, "*.*", 7 * MinutesPerDay));
        }
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-game-launcher-logs",
        NameEs = "Limpiar registros de launchers de juegos",
        NameEn = "Clean game launcher logs",
        DescriptionEs = "Borra registros de más de 7 días de Riot, EA, Epic y Ubisoft Connect.",
        DescriptionEn = "Deletes logs older than 7 days from Riot, EA, Epic and Ubisoft Connect.",
        TooltipEs = "Solo carpetas Logs de cada launcher, con más de 7 días. Los recientes se conservan por si hay que reportar un fallo. Mantenimiento no reversible.",
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
