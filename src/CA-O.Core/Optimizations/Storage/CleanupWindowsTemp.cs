using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes temp files older than 1 day: Windows Temp + service TEMP +
/// every interactive user's Temp (the service runs as SYSTEM, so %TEMP% alone
/// only covers its own profile). Files in use are skipped.</summary>
public sealed class CleanupWindowsTemp : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> BuildTargets()
    {
        List<(string Directory, string Pattern, int OlderThanDays)> list =
        [
            (@"%SystemRoot%\Temp", "*.*", 1),
            (@"%TEMP%", "*.*", 1),
        ];
        // PendingFiles() ya deduplica (seenDirs), omite inexistentes y solo
        // borra ficheros TopDirectoryOnly: añadir por perfil es seguro.
        foreach (var dir in InteractiveUserTempDirs()) list.Add((dir, "*.*", 1));
        return list;
    }

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-windows-temp",
        NameEs = "Limpiar archivos temporales de Windows",
        NameEn = "Clean Windows temporary files",
        DescriptionEs = "Borra temporales de Windows y de todos los usuarios con más de un día. Omite los que estén en uso.",
        DescriptionEn = "Deletes Windows and every user's temp files older than one day. Skips files in use.",
        TooltipEs = "Borra Windows\\Temp, TEMP del servicio y Temp de cada usuario. Mantenimiento no reversible.",
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
