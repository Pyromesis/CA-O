using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class RestoreLargeSendOffload : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-large-send-offload", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-large-send-offload",
        NameEs = "Restaurar Large Send Offload",
        NameEn = "Restaurar Large Send Offload",
        DescriptionEs = "Cuando NIC soporta. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Cuando NIC soporta.",
        TooltipEs = "restore-large-send-offload via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Restaurar Large Send Offload aplicado."));
    }
}
