using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>DNS errático o páginas que no cargan sin causa clara.</summary>
public sealed class RestartDnsClient : RestartWindowsServiceOptimization
{
    protected override string ServiceName => "Dnscache";
    protected override string ServiceLabel => "Cliente DNS";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restart-dns-client",
        NameEs = "Reiniciar cliente DNS",
        NameEn = "Restart DNS client",
        DescriptionEs = "Reinicia el cliente DNS de Windows. Solución habitual cuando hay errores de resolución.",
        DescriptionEn = "Restarts the Windows DNS client. Common fix for resolution errors.",
        TooltipEs = "Detiene e inicia Dnscache. Complementa vaciar la caché DNS.",
        Category = OptimizationCategory.Network,
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
