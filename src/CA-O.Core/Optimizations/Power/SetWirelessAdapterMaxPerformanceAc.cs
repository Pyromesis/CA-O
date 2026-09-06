using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>Sets the wireless adapter to maximum performance on AC via the active power scheme.</summary>
public sealed class SetWirelessAdapterMaxPerformanceAc : PowerAcSettingOptimization
{
    protected override string SubGuid => "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1";
    protected override string SettingGuid => "12bbebe6-58d6-4636-95bb-3217ef867c1a";
    protected override string TargetIndex => "0";

    public override OptimizationDefinition Definition => new()
    {
        Id = "set-wireless-adapter-max-performance-ac",
        NameEs = "Adaptador WiFi a máximo rendimiento en AC",
        NameEn = "Wireless adapter maximum performance on AC",
        DescriptionEs = "Pone el adaptador WiFi a máximo rendimiento en AC. Reduce latencia en red inalámbrica.",
        DescriptionEn = "Sets the wireless adapter to maximum performance on AC. Lowers wireless latency.",
        TooltipEs = "powercfg /setacvalueindex wireless + activación del plan. Solo AC. Reversible exacto.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };
}
