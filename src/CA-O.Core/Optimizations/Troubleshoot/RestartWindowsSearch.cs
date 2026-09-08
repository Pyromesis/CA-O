using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Buscador de Windows sin resultados o colgado.</summary>
public sealed class RestartWindowsSearch : RestartWindowsServiceOptimization
{
    protected override string ServiceName => "WSearch";
    protected override string ServiceLabel => "Búsqueda de Windows";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restart-windows-search",
        NameEs = "Reiniciar búsqueda de Windows",
        NameEn = "Restart Windows Search",
        DescriptionEs = "Reinicia la búsqueda de Windows. Solución habitual cuando no muestra resultados.",
        DescriptionEn = "Restarts Windows Search. Common fix for empty search results.",
        TooltipEs = "Detiene e inicia WSearch. No cambia la configuración de indexación.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible,
    };
}
