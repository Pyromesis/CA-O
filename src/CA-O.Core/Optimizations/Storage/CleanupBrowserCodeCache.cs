using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes regenerable browser code caches (Chrome/Edge Default +
/// classic Teams): Cache/Code Cache/GPUCache/ShaderCache only. NEVER Local
/// Storage, IndexedDB, Login Data or history: sessions live there. Files in
/// use (open browser) are skipped: close browsers first.</summary>
public sealed class CleanupBrowserCodeCache : TempFileCleanupOptimization
{
    private static readonly string[] SafeSubdirs = ["Cache", "Code Cache", "GPUCache", "ShaderCache"];
    private static readonly string[] TeamsSubdirs = ["Cache", "Code Cache", "GPUCache"];

    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> BuildTargets()
    {
        List<(string Directory, string Pattern, int OlderThanDays)> list = [];
        foreach (var sub in SafeSubdirs)
        {
            foreach (var dir in ProfileSubDirs("AppData", "Local", "Google", "Chrome", "User Data", "Default", sub))
                list.Add((dir, "*.*", 1));
            foreach (var dir in ProfileSubDirs("AppData", "Local", "Microsoft", "Edge", "User Data", "Default", sub))
                list.Add((dir, "*.*", 1));
        }
        foreach (var sub in TeamsSubdirs)
        {
            foreach (var dir in ProfileSubDirs("AppData", "Roaming", "Microsoft", "Teams", sub))
                list.Add((dir, "*.*", 1));
        }
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-browser-code-cache",
        NameEs = "Limpiar cachés de navegadores",
        NameEn = "Clean browser caches",
        DescriptionEs = "Vacía cachés regenerables de Chrome, Edge y Teams. Nunca toca sesiones, contraseñas ni historial. Cierra cada app antes.",
        DescriptionEn = "Empties regenerable Chrome, Edge and Teams caches. Never touches sessions, passwords or history. Close each app first.",
        TooltipEs = "Solo Cache/Code Cache/GPUCache/ShaderCache del perfil Default. Lo que esté en uso se omite. Mantenimiento no reversible.",
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
