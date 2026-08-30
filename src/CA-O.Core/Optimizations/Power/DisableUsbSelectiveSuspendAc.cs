using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

public sealed class DisableUsbSelectiveSuspendAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\disable-usb-selective-suspend-ac", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-usb-selective-suspend-ac",
        NameEs = "Desactivar suspension USB selectiva en AC",
        NameEn = "Desactivar suspension USB selectiva en AC",
        DescriptionEs = "Solo Competitive/LowLatency en AC, Conditional. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Solo Competitive/LowLatency en AC, Conditional.",
        TooltipEs = "disable-usb-selective-suspend-ac via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Desactivar suspension USB selectiva en AC aplicado."));
    }
}
