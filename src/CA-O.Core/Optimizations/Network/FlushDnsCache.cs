using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class FlushDnsCache : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\flush-dns-cache", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "flush-dns-cache",
        NameEs = "Vaciar cache DNS",
        NameEn = "Vaciar cache DNS",
        DescriptionEs = "Mantenimiento menor, sin impacto en juegos. Util para resolucion DNS inconsistente.",
        DescriptionEn = "Minor maintenance, no gaming impact. Useful for inconsistent DNS resolution.",
        TooltipEs = "flush-dns-cache via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Tiny,
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
        return Task.FromResult(OperationResult.Ok("Vaciar cache DNS aplicado."));
    }
}
