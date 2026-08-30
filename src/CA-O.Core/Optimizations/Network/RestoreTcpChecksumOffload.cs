using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class RestoreTcpChecksumOffload : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-tcp-checksum-offload", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-tcp-checksum-offload",
        NameEs = "Restaurar TCP checksum offload",
        NameEn = "Restaurar TCP checksum offload",
        DescriptionEs = "Restaura a estado soportado por driver. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Restaura a estado soportado por driver.",
        TooltipEs = "restore-tcp-checksum-offload via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Restaurar TCP checksum offload aplicado."));
    }
}
