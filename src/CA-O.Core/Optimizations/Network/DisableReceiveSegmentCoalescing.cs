using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// Desactiva Receive Segment Coalescing (*RscIPv4/*RscIPv6) en NICs físicas
/// que lo exponen. RSC junta varios paquetes recibidos en uno para ahorrar
/// CPU en descargas grandes; para tráfico de juego (paquetes pequeños y
/// frecuentes) solo añade espera hasta que se cierra el grupo. Coste: algo
/// más de CPU en descargas muy grandes. Reversible exacto.
/// </summary>
public sealed class DisableReceiveSegmentCoalescing : NicAdvancedPropertyOptimization
{
    protected override string[] PropertyNames { get; } = ["*RscIPv4", "*RscIPv6"];

    protected override string EnabledText => "0";
    protected override string DisabledText => "1";

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-receive-segment-coalescing",
        NameEs = "Paquetes sin agrupar al recibir (RSC)",
        NameEn = "No receive segment coalescing (RSC)",
        DescriptionEs = "La tarjeta de red entrega cada paquete al llegar en vez de esperar para agruparlos.",
        DescriptionEn = "The network card delivers each packet on arrival instead of waiting to batch them.",
        TooltipEs = "Desactiva *RscIPv4/*RscIPv6 solo en adaptadores físicos que lo exponen. Algo más de CPU en descargas muy grandes. Efecto al reiniciar el adaptador o el equipo. Reversible exacto.",
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
