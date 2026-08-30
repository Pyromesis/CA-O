using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.System;

public sealed class PendingRebootMaintenance : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\pending-reboot-maintenance", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "pending-reboot-maintenance",
        NameEs = "Mantenimiento reinicio pendiente",
        NameEn = "Mantenimiento reinicio pendiente",
        DescriptionEs = "Detecta Update/Servicing/driver reboot, evita interpretar antes de reinicio. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Detecta Update/Servicing/driver reboot, evita interpretar antes de reinicio.",
        TooltipEs = "pending-reboot-maintenance via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Mantenimiento reinicio pendiente aplicado."));
    }
}
