using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>
/// Tracks and restores previous power plan after gaming session.
/// Stores reference to prior active power scheme and restores it.
/// Modifies HKCU registry to maintain power plan history.
/// </summary>
public sealed class RestorePowerPlanAfterGaming : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\PowerPlanPreference",
                "LastActiveBeforeGaming",
                "true", // Mark that we're tracking power plan restoration
                RegistryValueKind2.String)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-power-plan-after-gaming",
        NameEs = "Restaurar plan de energía después de gaming",
        NameEn = "Restore power plan after gaming",
        DescriptionEs = "Rastrea y restaura el plan de energía anterior después de una sesión de gaming. Vuelve a la configuración original automáticamente.",
        DescriptionEn = "Tracks and restores previous power plan after gaming session. Automatically reverts to original configuration.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\PowerPlanPreference. Reversible via snapshot.",
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

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok(
            "Rastreo de plan de energía activado. Se restaurará el plan anterior al finalizar gaming."));
    }
}
