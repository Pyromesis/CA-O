using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

public sealed class DisablePcieLinkStatePowerSavingAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\disable-pcie-link-state-power-saving-ac", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-pcie-link-state-power-saving-ac",
        NameEs = "Desactivar ahorro PCIe en AC",
        NameEn = "Desactivar ahorro PCIe en AC",
        DescriptionEs = "Solo AC con GPU PCIe, Gaming/Latency. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Solo AC con GPU PCIe, Gaming/Latency.",
        TooltipEs = "disable-pcie-link-state-power-saving-ac via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Desactivar ahorro PCIe en AC aplicado."));
    }
}
