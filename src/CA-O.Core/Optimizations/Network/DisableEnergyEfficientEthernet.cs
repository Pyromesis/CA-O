using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// Desactiva Energy-Efficient Ethernet (802.3az) y sus variantes de
/// fabricante (Green Ethernet de Realtek, EEE Link Advertisement de Intel):
/// con el enlace en reposo la tarjeta duerme y el primer paquete tras una
/// pausa paga el despertar. Solo en adaptadores físicos que ya exponen la
/// propiedad; en todas ellas 0 = desactivado.
/// </summary>
public sealed class DisableEnergyEfficientEthernet : NicAdvancedPropertyOptimization
{
    protected override string[] PropertyNames { get; } =
        ["*EEE", "EEE", "AdvancedEEE", "EnableGreenEthernet", "GreenEthernet", "EEELinkAdvertisement"];

    protected override string EnabledText => "0";
    protected override string DisabledText => "1";

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-energy-efficient-ethernet",
        NameEs = "Ethernet sin ahorro de energía (EEE)",
        NameEn = "Ethernet without energy saving (EEE)",
        DescriptionEs = "Evita que la tarjeta de red cableada se duerma entre paquetes.",
        DescriptionEn = "Stops the wired network card from sleeping between packets.",
        TooltipEs = "Desactiva Energy-Efficient Ethernet / Green Ethernet solo en adaptadores físicos que lo exponen. Efecto al reiniciar el adaptador o el equipo. Reversible exacto.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.RequiresReboot,
    };
}
