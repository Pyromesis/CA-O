using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Caché del navegador integrado de Steam (htmlcache) y caché HTTP (appcache\httpcache). Es lo que el soporte de Steam pide borrar cuando la tienda no carga; los juegos y steamapps no se tocan.</summary>
public sealed class CleanupSteamWebCache : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        foreach (var dir in ProfileSubDirs("AppData", "Local", "Steam", "htmlcache"))
            list.Add((dir, "*.*", RealTempRetentionMinutes));
        foreach (var root in SteamRoots())
        {
            var http = Path.Combine(root, "appcache", "httpcache");
            if (Directory.Exists(http)) list.Add((http, "*.*", RealTempRetentionMinutes));
        }
        return list;
    }

    /// <summary>Instalaciones típicas de Steam (Program Files x86 y x64). Solo las que existen.</summary>
    private static IEnumerable<string> SteamRoots()
    {
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
        {
            string root;
            try { root = Path.Combine(Environment.GetFolderPath(folder), "Steam"); }
            catch { continue; }
            if (Directory.Exists(root)) yield return root;
        }
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-steam-web-cache",
        NameEs = "Limpiar caché web de Steam",
        NameEn = "Clean Steam web cache",
        DescriptionEs = "Vacía la caché web y HTTP del cliente de Steam. Nunca toca juegos ni descargas.",
        DescriptionEn = "Empties the Steam client's web and HTTP cache. Never touches games or downloads.",
        TooltipEs = "Solo htmlcache y appcache\\httpcache: la tienda y la biblioteca se recargan. Arregla una tienda que no carga. Cierra Steam antes. Mantenimiento no reversible.",
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
