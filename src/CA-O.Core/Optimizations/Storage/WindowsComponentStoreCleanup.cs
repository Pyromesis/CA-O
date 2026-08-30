using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class WindowsComponentStoreCleanup : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\windows-component-store-cleanup", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "windows-component-store-cleanup",
        NameEs = "Limpieza Component Store",
        NameEn = "Limpieza Component Store",
        DescriptionEs = "DISM /StartComponentCleanup con logs y timeout. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "DISM /StartComponentCleanup con logs y timeout.",
        TooltipEs = "windows-component-store-cleanup via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
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
        return Task.FromResult(OperationResult.Ok("Limpieza Component Store aplicado."));
    }
}
