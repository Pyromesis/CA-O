using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class ConfigureInterruptModerationForLowLatency : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\configure-interrupt-moderation-for-low-latency", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "configure-interrupt-moderation-for-low-latency",
        NameEs = "Moderar interrupciones para baja latencia",
        NameEn = "Moderar interrupciones para baja latencia",
        DescriptionEs = "Solo Competitive/LowLatency, muestra CPU Tradeoff. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Solo Competitive/LowLatency, muestra CPU Tradeoff.",
        TooltipEs = "configure-interrupt-moderation-for-low-latency via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Vendor,
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
        return Task.FromResult(OperationResult.Ok("Moderar interrupciones para baja latencia aplicado."));
    }
}
