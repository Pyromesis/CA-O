using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

/// <summary>Disables non-essential third-party startup entries (Run keys).</summary>
public sealed class DisableUnnecessaryStartupApps : StartupRunKeyOptimization
{
    protected override bool ShouldDisable(string name, string command, string? company)
    {
        // Todo lo no protegido es innecesario por defecto; la base ya excluyó
        // antivirus, drivers, Microsoft y componentes del sistema.
        return true;
    }

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-unnecessary-startup-apps",
        NameEs = "Desactivar apps inicio innecesarias",
        NameEn = "Disable unnecessary startup apps",
        DescriptionEs = "Desactiva entradas de inicio de terceros no esenciales. Excluye antivirus, drivers y Microsoft.",
        DescriptionEn = "Disables non-essential third-party startup entries. Excludes antivirus, drivers and Microsoft.",
        TooltipEs = "Elimina valores en HKCU/HKLM Run tras clasificar publicador. Reversible exacto.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };
}
