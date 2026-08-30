using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class EnsureTrimEnabled : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\ensure-trim-enabled", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "ensure-trim-enabled",
        NameEs = "Asegurar TRIM habilitado",
        NameEn = "Asegurar TRIM habilitado",
        DescriptionEs = "Si TRIM desactivado en SSD NTFS compatible, Recommended. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Si TRIM desactivado en SSD NTFS compatible, Recommended.",
        TooltipEs = "ensure-trim-enabled via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Asegurar TRIM habilitado aplicado."));
    }
}
