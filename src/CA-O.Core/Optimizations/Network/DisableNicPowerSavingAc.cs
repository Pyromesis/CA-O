using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class DisableNicPowerSavingAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\disable-nic-power-saving-ac", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-nic-power-saving-ac",
        NameEs = "Desactivar ahorro energia NIC en AC",
        NameEn = "Desactivar ahorro energia NIC en AC",
        DescriptionEs = "Solo AC gaming/low latency y NIC compatible. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Solo AC gaming/low latency y NIC compatible.",
        TooltipEs = "disable-nic-power-saving-ac via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Desactivar ahorro energia NIC en AC aplicado."));
    }
}
