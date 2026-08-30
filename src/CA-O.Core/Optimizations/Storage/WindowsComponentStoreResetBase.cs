using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class WindowsComponentStoreResetBase : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\windows-component-store-resetbase", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "windows-component-store-resetbase",
        NameEs = "ResetBase Component Store",
        NameEn = "ResetBase Component Store",
        DescriptionEs = "DISM /ResetBase, Expert, Irreversible, HighImpact. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "DISM /ResetBase, Expert, Irreversible, HighImpact.",
        TooltipEs = "windows-component-store-resetbase via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Moderate,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.High,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("ResetBase Component Store aplicado."));
    }
}
