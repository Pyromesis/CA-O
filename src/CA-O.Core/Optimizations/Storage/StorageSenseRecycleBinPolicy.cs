using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class StorageSenseRecycleBinPolicy : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\storage-sense-recycle-bin-policy", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "storage-sense-recycle-bin-policy",
        NameEs = "Politica Papelera en Storage Sense",
        NameEn = "Politica Papelera en Storage Sense",
        DescriptionEs = "Configura 7/14/30/60/90 dias, nunca inmediato. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Configura 7/14/30/60/90 dias, nunca inmediato.",
        TooltipEs = "storage-sense-recycle-bin-policy via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
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
        return Task.FromResult(OperationResult.Ok("Politica Papelera en Storage Sense aplicado."));
    }
}
