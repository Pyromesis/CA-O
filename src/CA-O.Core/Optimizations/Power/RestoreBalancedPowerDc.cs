using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>Activates the Balanced power plan to save battery.</summary>
public sealed class RestoreBalancedPowerDc : PowerSchemeSwitchOptimization
{
    protected override string TargetScheme => "SCHEME_BALANCED";
    protected override string TargetLabel => "Equilibrado";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-balanced-power-dc",
        NameEs = "Restaurar modo Equilibrado en batería",
        NameEn = "Restore Balanced mode on battery",
        DescriptionEs = "Activa el plan Equilibrado con powercfg para ahorrar batería.",
        DescriptionEn = "Activates the Balanced plan via powercfg to save battery.",
        TooltipEs = "Ejecuta powercfg /setactive SCHEME_BALANCED y restaura el previo al revertir.",
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
