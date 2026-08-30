using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.System;

public sealed class OptimizeStartupRecoveryState : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\optimize-startup-recovery-state", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "optimize-startup-recovery-state",
        NameEs = "Auditar recuperacion de inicio",
        NameEn = "Auditar recuperacion de inicio",
        DescriptionEs = "Detecta timeout, entradas invalidas, solo Detect/Explain. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Detecta timeout, entradas invalidas, solo Detect/Explain.",
        TooltipEs = "optimize-startup-recovery-state via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.None,
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
        return Task.FromResult(OperationResult.Ok("Auditar recuperacion de inicio aplicado."));
    }
}
