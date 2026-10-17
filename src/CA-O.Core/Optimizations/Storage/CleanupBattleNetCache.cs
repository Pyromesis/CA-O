using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Caché del cliente de Battle.net (Cache y BrowserCache en AppData\Local). Es el paso de 'borrar caché' del soporte de Blizzard; los juegos no se tocan.</summary>
public sealed class CleanupBattleNetCache : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        foreach (var sub in new[] { "Cache", "BrowserCache" })
        {
            foreach (var dir in ProfileSubDirs("AppData", "Local", "Battle.net", sub))
                list.Add((dir, "*.*", RealTempRetentionMinutes));
        }
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-battlenet-cache",
        NameEs = "Limpiar caché de Battle.net",
        NameEn = "Clean Battle.net cache",
        DescriptionEs = "Vacía la caché del cliente de Battle.net. Nunca toca juegos instalados.",
        DescriptionEn = "Empties the Battle.net client cache. Never touches installed games.",
        TooltipEs = "Solo Battle.net\\Cache y BrowserCache del perfil: arregla un cliente que no carga. Cierra Battle.net antes. Mantenimiento no reversible.",
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
