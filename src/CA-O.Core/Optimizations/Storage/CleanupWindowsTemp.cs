using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes temp files older than 1 day: Windows Temp + service TEMP/TMP +
/// every interactive user's Temp (the service runs as SYSTEM, so %TEMP% alone
/// only covers its own profile) + per-user regenerable caches (LocalLow Temp,
/// INetCache, D3DSCache, CryptnetUrlCache). Files in use are skipped.</summary>
public sealed class CleanupWindowsTemp : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> BuildTargets()
    {
        List<(string Directory, string Pattern, int OlderThanDays)> list =
        [
            (@"%SystemRoot%\Temp", "*.*", 1),
            (@"%TEMP%", "*.*", 1),
            (@"%TMP%", "*.*", 1),
        ];
        // PendingFiles() ya deduplica (seenDirs), omite inexistentes y borra
        // ficheros recursivos (AllDirectories): añadir por perfil es seguro.
        foreach (var dir in InteractiveUserTempDirs()) list.Add((dir, "*.*", 1));
        foreach (var dir in ProfileSubDirs("AppData", "LocalLow", "Temp")) list.Add((dir, "*.*", 1));
        foreach (var dir in ProfileSubDirs("AppData", "Local", "Microsoft", "Windows", "INetCache")) list.Add((dir, "*.*", 1));
        foreach (var dir in ProfileSubDirs("AppData", "Local", "D3DSCache")) list.Add((dir, "*.*", 1));
        foreach (var dir in ProfileSubDirs("AppData", "LocalLow", "Microsoft", "CryptnetUrlCache")) list.Add((dir, "*.*", 1));
        return list;
    }

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-windows-temp",
        NameEs = "Limpiar archivos temporales de Windows",
        NameEn = "Clean Windows temporary files",
        DescriptionEs = "Borra temporales de Windows, servicio y todos los usuarios (TEMP/TMP, LocalLow) más cachés regenerables (INetCache, D3DSCache, CryptnetUrlCache) con más de un día. Omite los que estén en uso.",
        DescriptionEn = "Deletes Windows, service and every user's temp files (TEMP/TMP, LocalLow) plus regenerable caches (INetCache, D3DSCache, CryptnetUrlCache) older than one day. Skips files in use.",
        TooltipEs = "Borra Windows\\Temp, TEMP/TMP del servicio y Temp de cada usuario + cachés regenerables por perfil. Recursivo. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
