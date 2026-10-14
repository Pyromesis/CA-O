using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Deletes Outlook secure-temp attachments (INetCache\Content.Outlook)
/// older than 7 days for every profile. Locked files are skipped.</summary>
public sealed class CleanupOutlookCache : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets => BuildTargets();

    private static IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> BuildTargets()
    {
        // CONSERVA 7 días (no entra en la retención de 10 minutos): son
        // adjuntos que el usuario puede reabrir desde el correo durante días;
        // borrarlos a los 10 minutos rompería el flujo de "abrir adjunto".
        List<(string Directory, string Pattern, int OlderThanMinutes)> list = [];
        foreach (var dir in ProfileSubDirs("AppData", "Local", "Microsoft", "Windows", "INetCache", "Content.Outlook"))
            list.Add((dir, "*.*", 7 * MinutesPerDay));
        return list;
    }

    protected override string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-outlook-cache",
        NameEs = "Limpiar caché de Outlook",
        NameEn = "Clean Outlook cache",
        DescriptionEs = "Borra adjuntos temporales de Outlook (Content.Outlook) de más de 7 días, de todos los usuarios.",
        DescriptionEn = "Deletes Outlook temp attachments (Content.Outlook) older than 7 days, for every user.",
        TooltipEs = "Adjuntos que Outlook guarda al abrirlos. Se regeneran solos. Mantenimiento no reversible.",
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
