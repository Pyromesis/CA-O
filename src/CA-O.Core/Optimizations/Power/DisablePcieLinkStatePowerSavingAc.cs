using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>Disables PCIe Link State Power Management on AC via the active power scheme.</summary>
public sealed class DisablePcieLinkStatePowerSavingAc : PowerAcSettingOptimization
{
    protected override string SubGuid => "501a4d13-42af-4429-9fd1-a8218c268e1d";
    protected override string SettingGuid => "ee12f906-d277-4bcf-ad6c-e5a569d7c83d";
    protected override string TargetIndex => "0";

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-pcie-link-state-power-saving-ac",
        NameEs = "Deshabilitar ahorro de energía en PCIe en AC",
        NameEn = "Disable PCIe Link State Power Saving on AC",
        DescriptionEs = "Deshabilita el ahorro de enlace PCIe en AC. Evita que la GPU reduzca velocidad.",
        DescriptionEn = "Disables PCIe Link State saving on AC. Keeps the GPU at full speed.",
        TooltipEs = "powercfg /setacvalueindex PCIEXPRESS + activación del plan. Solo AC. Reversible exacto.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };
}
