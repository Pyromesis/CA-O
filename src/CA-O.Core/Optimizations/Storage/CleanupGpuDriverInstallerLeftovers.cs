using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Carpetas de extracción de los instaladores de NVIDIA (C:\NVIDIA) y AMD (C:\AMD): copias desempaquetadas de instalaciones pasadas. El driver activo vive en DriverStore y no se toca. Solo ficheros de más de 1 día.</summary>
public sealed class CleanupGpuDriverInstallerLeftovers : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static List<(string, string, int)> BuildTargets()
    {
        List<(string, string, int)> list = [];
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        foreach (var name in new[] { "NVIDIA", "AMD" })
        {
            var dir = Path.Combine(root, name);
            if (Directory.Exists(dir)) list.Add((dir, "*.*", MinutesPerDay));
        }
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-gpu-driver-installer-leftovers",
        NameEs = "Limpiar restos de instaladores de drivers GPU",
        NameEn = "Clean GPU driver installer leftovers",
        DescriptionEs = "Borra los instaladores ya extraídos que NVIDIA y AMD dejan en C:\\NVIDIA y C:\\AMD.",
        DescriptionEn = "Deletes the extracted installers NVIDIA and AMD leave in C:\\NVIDIA and C:\\AMD.",
        TooltipEs = "Son copias desempaquetadas de instalaciones anteriores (cientos de MB). El driver instalado vive en Windows y no se toca. Mantenimiento no reversible.",
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
