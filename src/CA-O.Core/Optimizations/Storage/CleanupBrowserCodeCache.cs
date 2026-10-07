using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes regenerable browser code caches (Chrome/Edge every profile +
/// classic Teams): Cache/Code Cache/GPUCache/ShaderCache only. NEVER Local
/// Storage, IndexedDB, Login Data or history: sessions live there. Files in
/// use (open browser) are skipped: close browsers first.</summary>
public sealed class CleanupBrowserCodeCache : TempFileCleanupOptimization
{
    private static readonly string[] SafeSubdirs = ["Cache", "Code Cache", "GPUCache", "ShaderCache"];
    private static readonly string[] TeamsSubdirs = ["Cache", "Code Cache", "GPUCache"];

    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets => BuildTargets();

    /// <summary>
    /// Caches regenerables de TODOS los perfiles de un directorio "User Data".
    /// CAO-BUG-2026-10-06: antes se recorria solo el perfil "Default" fijo. Un
    /// perfil real no se llama siempre Default: Chrome y Edge crean "Profile 1",
    /// "Profile 2"... para el resto. Cubriendo solo "Default", el navegador que el
    /// usuario usa a diario se quedaba sin limpiar, y como el unico perfil
    /// cubierto no tenia nada que borrar el optimizador reportaba exito
    /// indefinidamente, igual que un no-op. Basta con recorrer los subdirectorios
    /// y quedarse con los que contienen alguna cache regenerable: asi se excluyen
    /// tambien las carpetas propias del navegador (Crashpad, ShaderCache,
    /// GraphiteDawnCache...) que no son perfiles.
    /// </summary>
    internal static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> CacheTargetsUnder(string userDataDirectory)
    {
        List<(string Directory, string Pattern, int OlderThanDays)> list = [];
        if (!Directory.Exists(userDataDirectory))
            return list;
        foreach (var profile in Directory.GetDirectories(userDataDirectory))
        {
            foreach (var sub in SafeSubdirs)
            {
                var dir = Path.Combine(profile, sub);
                if (Directory.Exists(dir))
                    list.Add((dir, "*.*", 1));
            }
        }
        return list;
    }

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> BuildTargets()
    {
        List<(string Directory, string Pattern, int OlderThanDays)> list = [];
        foreach (var userData in ProfileSubDirs("AppData", "Local", "Google", "Chrome", "User Data"))
            list.AddRange(CacheTargetsUnder(userData));
        foreach (var userData in ProfileSubDirs("AppData", "Local", "Microsoft", "Edge", "User Data"))
            list.AddRange(CacheTargetsUnder(userData));
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
        TooltipEs = "Solo Cache/Code Cache/GPUCache/ShaderCache de todos los perfiles de Chrome, Edge y Teams. Lo que esté en uso se omite. Mantenimiento no reversible.",
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
