using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

public sealed class DisableSelectedThirdPartyBackgroundTask : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\disable-selected-third-party-background-task", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-selected-third-party-background-task",
        NameEs = "Desactivar tareas segundo plano terceros",
        NameEn = "Desactivar tareas segundo plano terceros",
        DescriptionEs = "Disable con rollback, excluye Update/Defender. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Disable con rollback, excluye Update/Defender.",
        TooltipEs = "disable-selected-third-party-background-task via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
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
        return Task.FromResult(OperationResult.Ok("Desactivar tareas segundo plano terceros aplicado."));
    }
}
