using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class RestoreUdpChecksumOffload : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-udp-checksum-offload", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-udp-checksum-offload",
        NameEs = "Restaurar UDP checksum offload",
        NameEn = "Restaurar UDP checksum offload",
        DescriptionEs = "Restaura a estado soportado. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Restaura a estado soportado.",
        TooltipEs = "restore-udp-checksum-offload via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Restaurar UDP checksum offload aplicado."));
    }
}
