using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class EnableStorageSense : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\enable-storage-sense", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-storage-sense",
        NameEs = "Activar Storage Sense",
        NameEn = "Activar Storage Sense",
        DescriptionEs = "Solo si usuario desea y poco espacio. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Solo si usuario desea y poco espacio.",
        TooltipEs = "enable-storage-sense via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Activar Storage Sense aplicado."));
    }
}
