using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class ResetNetworkStackRepair : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\reset-network-stack-repair", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "reset-network-stack-repair",
        NameEs = "Reparar pila de red",
        NameEn = "Reparar pila de red",
        DescriptionEs = "Winsock/TCP/DNS solo si sintomas. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Winsock/TCP/DNS solo si sintomas.",
        TooltipEs = "reset-network-stack-repair via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Reparar pila de red aplicado."));
    }
}
