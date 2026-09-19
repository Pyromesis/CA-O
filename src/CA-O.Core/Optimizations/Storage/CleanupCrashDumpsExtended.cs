using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Extended crash-dump maintenance: LiveKernelReports (*.dmp >30d),
/// MEMORY.DMP (>30d) and per-user CrashDumps. Destructive for debugging:
/// the UI asks for confirmation (ConfirmIfNeededAsync).</summary>
public sealed class CleanupCrashDumpsExtended : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> BuildTargets()
    {
        List<(string Directory, string Pattern, int OlderThanDays)> list =
        [
            (@"%SystemRoot%\LiveKernelReports", "*.dmp", 30),
            (@"%SystemRoot%", "MEMORY.DMP", 30),
        ];
        foreach (var dir in ProfileSubDirs("AppData", "Local", "CrashDumps")) list.Add((dir, "*.dmp", 30));
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-crash-dumps-extended",
        NameEs = "Limpiar volcados extendidos",
        NameEn = "Clean extended crash dumps",
        DescriptionEs = "Borra LiveKernelReports, MEMORY.DMP y CrashDumps de usuario de más de 30 días. Pide confirmación.",
        DescriptionEn = "Deletes 30+ day LiveKernelReports, MEMORY.DMP and per-user CrashDumps. Asks for confirmation.",
        TooltipEs = "Volcados del sistema y de apps de +30 días. Dificulta depurar fallos viejos: pide confirmación. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
        Flags = OptimizationFlags.NotReversible,
    };
}
