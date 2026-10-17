using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Registros de servicio en Windows\Logs (DISM, WindowsUpdate, NetSetup, SIH, waasmedic) con más de 30 días. Se excluyen CBS (limpieza propia) y MeasuredBoot (atestación).</summary>
public sealed class CleanupOldSystemLogs : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        foreach (var sub in new[] { "DISM", "WindowsUpdate", "NetSetup", "SIH", "waasmedic" })
            list.Add((@"%SystemRoot%\Logs\" + sub, "*.*", 30 * MinutesPerDay));
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-old-system-logs",
        NameEs = "Limpiar registros antiguos del sistema",
        NameEn = "Clean old system logs",
        DescriptionEs = "Borra registros de DISM, Windows Update y configuración de red con más de 30 días.",
        DescriptionEn = "Deletes DISM, Windows Update and network setup logs older than 30 days.",
        TooltipEs = "Solo Windows\\Logs\\DISM, WindowsUpdate, NetSetup, SIH y waasmedic, con más de 30 días. Nunca CBS (tiene su propia limpieza) ni Measured Boot. Mantenimiento no reversible.",
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
