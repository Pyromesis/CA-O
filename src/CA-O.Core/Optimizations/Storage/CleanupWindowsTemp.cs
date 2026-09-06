using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes system temp files older than 1 day (Windows Temp + service TEMP).</summary>
public sealed class CleanupWindowsTemp : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; } =
    [
        (@"%SystemRoot%\Temp", "*.*", 1),
        (@"%TEMP%", "*.*", 1),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-windows-temp",
        NameEs = "Limpiar archivos temporales de Windows",
        NameEn = "Cleanup Windows temporary files",
        DescriptionEs = "Borra ficheros temporales del sistema con más de un día. Omite los que estén en uso.",
        DescriptionEn = "Deletes system temp files older than one day. Skips files in use.",
        TooltipEs = "Borra en la carpeta Temp de Windows y TEMP del servicio. Mantenimiento no reversible.",
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
