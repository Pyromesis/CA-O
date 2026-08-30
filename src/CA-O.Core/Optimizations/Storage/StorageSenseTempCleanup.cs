using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class StorageSenseTempCleanup : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\storage-sense-temp-cleanup", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "storage-sense-temp-cleanup",
        NameEs = "Limpieza automaticas de temporales",
        NameEn = "Limpieza automaticas de temporales",
        DescriptionEs = "Activa limpieza de temporales, no toca Downloads. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Activa limpieza de temporales, no toca Downloads.",
        TooltipEs = "storage-sense-temp-cleanup via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Limpieza automaticas de temporales aplicado."));
    }
}
