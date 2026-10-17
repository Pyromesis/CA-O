using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Cachés de descarga de gestores de paquetes (npm-cache, pip\Cache, Yarn\Cache) de cada usuario: los propios npm cache clean / pip cache purge las vacían igual. Nunca proyectos ni node_modules. Ficheros de más de 1 día.</summary>
public sealed class CleanupDeveloperCaches : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        string[][] caches =
        [
            ["AppData", "Local", "npm-cache"],
            ["AppData", "Roaming", "npm-cache"],
            ["AppData", "Local", "pip", "Cache"],
            ["AppData", "Local", "Yarn", "Cache"],
        ];
        foreach (var parts in caches)
        {
            foreach (var dir in ProfileSubDirs(parts))
                list.Add((dir, "*.*", MinutesPerDay));
        }
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-developer-caches",
        NameEs = "Limpiar cachés de desarrollo (npm, pip, Yarn)",
        NameEn = "Clean developer caches (npm, pip, Yarn)",
        DescriptionEs = "Vacía las cachés de paquetes de npm, pip y Yarn. Se vuelven a descargar al instalar.",
        DescriptionEn = "Empties npm, pip and Yarn package caches. They are downloaded again on install.",
        TooltipEs = "Solo cachés de descarga (npm-cache, pip\\Cache, Yarn\\Cache), nunca proyectos ni node_modules. Pueden ser varios GB. Mantenimiento no reversible.",
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
