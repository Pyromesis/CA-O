using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class RestoreSystemManagedPagefile : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-system-managed-pagefile", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-system-managed-pagefile",
        NameEs = "Restaurar pagefile administrado",
        NameEn = "Restaurar pagefile administrado",
        DescriptionEs = "Si fijado arbitrariamente, ofrece System Managed. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Si fijado arbitrariamente, ofrece System Managed.",
        TooltipEs = "restore-system-managed-pagefile via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
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
        return Task.FromResult(OperationResult.Ok("Restaurar pagefile administrado aplicado."));
    }
}
