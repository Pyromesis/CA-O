using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Cola de impresión atascada: trabajos que no salen ni cancelando.</summary>
public sealed class RestartPrintSpooler : RestartWindowsServiceOptimization
{
    protected override string ServiceName => "Spooler";
    protected override string ServiceLabel => "Cola de impresión";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restart-print-spooler",
        NameEs = "Reiniciar cola de impresión",
        NameEn = "Restart print spooler",
        DescriptionEs = "Reinicia la cola de impresión. Solución habitual cuando los documentos se atascan.",
        DescriptionEn = "Restarts the print spooler. Common fix for stuck print jobs.",
        TooltipEs = "Detiene e inicia el servicio Spooler. No borra impresoras ni cambia configuración.",
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
