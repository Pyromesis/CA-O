using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Extended crash-dump maintenance: system Minidump (*.dmp >30d,
/// fusionado desde stale-crash-dump-cleanup, auditoría 2026-09-29),
/// LiveKernelReports (*.dmp >30d), MEMORY.DMP (>30d) and per-user
/// CrashDumps. Destructive for debugging: the UI asks for confirmation
/// (ConfirmIfNeededAsync).</summary>
public sealed class CleanupCrashDumpsExtended : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> BuildTargets()
    {
        // CONSERVA 30 días (no entra en la retención de 10 minutos): los
        // volcados son evidencia forense/diagnóstica; borrarlos pronto
        // impediría depurar fallos pasados. La UI pide confirmación.
        List<(string Directory, string Pattern, int OlderThanMinutes)> list =
        [
            (@"%SystemRoot%\Minidump", "*.dmp", 30 * MinutesPerDay),
            (@"%SystemRoot%\LiveKernelReports", "*.dmp", 30 * MinutesPerDay),
            (@"%SystemRoot%", "MEMORY.DMP", 30 * MinutesPerDay),
        ];
        foreach (var dir in ProfileSubDirs("AppData", "Local", "CrashDumps")) list.Add((dir, "*.dmp", 30 * MinutesPerDay));
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-crash-dumps-extended",
        NameEs = "Limpiar volcados extendidos",
        NameEn = "Clean extended crash dumps",
        DescriptionEs = "Borra Minidump, LiveKernelReports, MEMORY.DMP y CrashDumps de usuario de más de 30 días. Pide confirmación.",
        DescriptionEn = "Deletes 30+ day Minidump, LiveKernelReports, MEMORY.DMP and per-user CrashDumps. Asks for confirmation.",
        TooltipEs = "Volcados del sistema y de apps de +30 días (incluye Minidump). Dificulta depurar fallos viejos: pide confirmación. Mantenimiento no reversible.",
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
