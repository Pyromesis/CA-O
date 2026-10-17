using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Carpetas webcache* del launcher de Epic Games: el soporte de Epic indica borrarlas cuando el launcher se queda en blanco o va lento. Ni juegos ni manifiestos.</summary>
public sealed class CleanupEpicLauncherCache : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        foreach (var saved in ProfileSubDirs("AppData", "Local", "EpicGamesLauncher", "Saved"))
        {
            string[] dirs;
            try { dirs = Directory.GetDirectories(saved, "webcache*"); }
            catch { continue; }
            foreach (var dir in dirs) list.Add((dir, "*.*", RealTempRetentionMinutes));
        }
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-epic-launcher-cache",
        NameEs = "Limpiar caché de Epic Games Launcher",
        NameEn = "Clean Epic Games Launcher cache",
        DescriptionEs = "Vacía la caché web del launcher de Epic. Nunca toca juegos instalados.",
        DescriptionEn = "Empties the Epic launcher web cache. Never touches installed games.",
        TooltipEs = "Solo carpetas webcache* de EpicGamesLauncher\\Saved: arregla un launcher lento o en blanco. Cierra Epic antes. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
