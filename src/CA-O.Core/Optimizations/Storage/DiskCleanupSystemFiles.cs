using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class DiskCleanupSystemFiles : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\disk-cleanup-system-files", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disk-cleanup-system-files",
        NameEs = "Liberador espacio archivos sistema",
        NameEn = "Liberador espacio archivos sistema",
        DescriptionEs = "Via cleanmgr categorias soportadas, no borra manual critico. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Via cleanmgr categorias soportadas, no borra manual critico.",
        TooltipEs = "disk-cleanup-system-files via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Liberador espacio archivos sistema aplicado."));
    }
}
