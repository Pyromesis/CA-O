using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>Returns to the Balanced plan after a gaming session.</summary>
public sealed class RestorePowerPlanAfterGaming : PowerSchemeSwitchOptimization
{
    protected override string TargetScheme => "SCHEME_BALANCED";
    protected override string TargetLabel => "Equilibrado";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-power-plan-after-gaming",
        NameEs = "Restaurar plan de energía después de gaming",
        NameEn = "Restore power plan after gaming",
        DescriptionEs = "Vuelve al plan Equilibrado tras una sesión de gaming. Guarda el previo para revertir.",
        DescriptionEn = "Returns to the Balanced plan after a gaming session. Keeps the previous one for revert.",
        TooltipEs = "Ejecuta powercfg /setactive SCHEME_BALANCED y restaura el previo al revertir.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };
}
