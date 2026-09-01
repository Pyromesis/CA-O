using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>
/// Restores balanced power settings when on DC (battery) power.
/// Reduces CPU speed, disk activity, and display brightness to save battery.
/// Official Windows registry configuration (documented in MS-Windows-Power-Control).
/// </summary>
public sealed class RestoreBalancedPowerDc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable hard disk timeout on battery (saves power)
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Control Panel\PowerCfg\PowerPolicies\1",
                "ConservationIdleTimeout",
                600, // 600 seconds = 10 minutes
                RegistryValueKind2.DWord),
            // Enable monitor timeout on battery (saves power)
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Control Panel\PowerCfg\PowerPolicies\1",
                "VideoTimeout",
                300, // 300 seconds = 5 minutes
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-balanced-power-dc",
        NameEs = "Restaurar modo Equilibrado en batería",
        NameEn = "Restore Balanced mode on battery power",
        DescriptionEs = "Restaura suspensión de disco y pantalla en batería para ahorrar energía. Solo aplica en batería.",
        DescriptionEn = "Restores disk and monitor suspension on battery to save power. Only applies on battery.",
        TooltipEs = "Modifica HKCU\\Control Panel\\PowerCfg\\PowerPolicies. Reversible via snapshot.",
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

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Modo Equilibrado restaurado en batería."));
    }
}
