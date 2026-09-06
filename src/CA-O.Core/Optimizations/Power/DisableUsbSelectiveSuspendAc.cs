using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>Disables USB selective suspend on AC via the active power scheme.</summary>
public sealed class DisableUsbSelectiveSuspendAc : PowerAcSettingOptimization
{
    protected override string SubGuid => "2a737441-1930-4402-8d77-b2bebba308a3";
    protected override string SettingGuid => "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";
    protected override string TargetIndex => "0";

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-usb-selective-suspend-ac",
        NameEs = "Deshabilitar suspensión selectiva USB en AC",
        NameEn = "Disable USB selective suspend on AC",
        DescriptionEs = "Deshabilita la suspensión selectiva USB en AC. Evita que ratones y teclados se duerman.",
        DescriptionEn = "Disables USB selective suspend on AC. Keeps mice and keyboards awake.",
        TooltipEs = "powercfg /setacvalueindex USB + activación del plan. Solo AC. Reversible exacto.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };
}
