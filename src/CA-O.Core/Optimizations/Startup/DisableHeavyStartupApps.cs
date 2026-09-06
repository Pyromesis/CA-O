using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

/// <summary>Disables heavyweight startup entries (updaters, launchers, chat/music clients).</summary>
public sealed class DisableHeavyStartupApps : StartupRunKeyOptimization
{
    private static readonly string[] HeavyKeywords =
        ["update", "updater", "launcher", "helper", "tray", "agent", "discord", "spotify",
         "teams", "steam", "adobe", "creative", "electron", "slack", "zoom", "skype", "dropbox"];

    protected override bool ShouldDisable(string name, string command, string? company)
    {
        var haystack = (name + " " + command).ToLowerInvariant();
        return HeavyKeywords.Any(k => haystack.Contains(k));
    }

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-heavy-startup-apps",
        NameEs = "Desactivar apps inicio pesadas",
        NameEn = "Disable heavy startup apps",
        DescriptionEs = "Desactiva actualizadores, lanzadores y clientes pesados del inicio. Excluye antivirus y drivers.",
        DescriptionEn = "Disables updaters, launchers and heavy clients at startup. Excludes antivirus and drivers.",
        TooltipEs = "Elimina valores en HKCU/HKLM Run que coinciden con patrones pesados. Reversible exacto.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };
}
