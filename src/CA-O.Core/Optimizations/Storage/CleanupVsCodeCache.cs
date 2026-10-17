using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Cachés regenerables de Visual Studio Code e Insiders (Cache, CachedData, Code Cache, GPUCache) y registros de más de 7 días. Extensiones, ajustes y espacios de trabajo no se tocan.</summary>
public sealed class CleanupVsCodeCache : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        foreach (var product in new[] { "Code", "Code - Insiders" })
        {
            foreach (var sub in new[] { "Cache", "CachedData", "Code Cache", "GPUCache" })
            {
                foreach (var dir in ProfileSubDirs("AppData", "Roaming", product, sub))
                    list.Add((dir, "*.*", MinutesPerDay));
            }
            foreach (var dir in ProfileSubDirs("AppData", "Roaming", product, "logs"))
                list.Add((dir, "*.*", 7 * MinutesPerDay));
        }
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-vscode-cache",
        NameEs = "Limpiar cachés de Visual Studio Code",
        NameEn = "Clean Visual Studio Code caches",
        DescriptionEs = "Vacía las cachés regenerables de VS Code (y Insiders). Nunca extensiones, ajustes ni proyectos.",
        DescriptionEn = "Empties regenerable VS Code (and Insiders) caches. Never extensions, settings or projects.",
        TooltipEs = "Solo Cache, CachedData, Code Cache, GPUCache y logs de más de 7 días. Cierra VS Code antes o se omite lo que esté en uso. Mantenimiento no reversible.",
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
