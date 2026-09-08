using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Bluetooth que no conecta o desaparece tras suspender.</summary>
public sealed class RestartBluetoothService : RestartWindowsServiceOptimization
{
    protected override string ServiceName => "bthserv";
    protected override string ServiceLabel => "Servicio Bluetooth";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restart-bluetooth-service",
        NameEs = "Reiniciar servicio Bluetooth",
        NameEn = "Restart Bluetooth service",
        DescriptionEs = "Reinicia el servicio Bluetooth. Solución habitual cuando no empareja o desaparece.",
        DescriptionEn = "Restarts the Bluetooth service. Common fix for pairing failures.",
        TooltipEs = "Detiene e inicia bthserv. Los dispositivos emparejados se conservan.",
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
