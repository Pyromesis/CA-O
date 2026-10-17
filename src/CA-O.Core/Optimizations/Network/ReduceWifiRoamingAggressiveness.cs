using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// Roaming Aggressiveness al mínimo (propiedad de adaptadores Wi-Fi Intel,
/// 0 = Lowest, por defecto 2 = Medium). Con valores medios o altos la tarjeta
/// escanea otros puntos de acceso en cuanto la señal baja un poco, y cada
/// escaneo es un hueco de decenas de milisegundos: picos de ping en mitad de
/// la partida aunque no cambie de router. Solo en adaptadores que exponen la
/// propiedad. Contrapartida: en casas grandes con varios AP tarda más en
/// pasarse al más cercano. Reversible exacto.
/// </summary>
public sealed class ReduceWifiRoamingAggressiveness : NicAdvancedPropertyOptimization
{
    protected override string[] PropertyNames { get; } = ["RoamAggressiveness"];

    protected override string EnabledText => "0";
    protected override string DisabledText => "2";

    public override OptimizationDefinition Definition => new()
    {
        Id = "reduce-wifi-roaming-aggressiveness",
        NameEs = "Wi-Fi: menos búsquedas de roaming",
        NameEn = "Wi-Fi: less roaming scans",
        DescriptionEs = "La tarjeta Wi-Fi deja de buscar otros puntos de acceso a la mínima, que provoca picos de ping en partida.",
        DescriptionEn = "The Wi-Fi card stops scanning for other access points at the slightest dip, which causes in-game ping spikes.",
        TooltipEs = "Roaming Aggressiveness al mínimo en adaptadores Intel que lo exponen. Con varios puntos de acceso (malla) tardará más en cambiar al más cercano. Reversible exacto.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };
}
