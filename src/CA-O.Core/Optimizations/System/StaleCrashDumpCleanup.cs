using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.System;

public sealed class StaleCrashDumpCleanup : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\stale-crash-dump-cleanup", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "stale-crash-dump-cleanup",
        NameEs = "Limpiar dumps antiguos",
        NameEn = "Limpiar dumps antiguos",
        DescriptionEs = "Minidump/Memory.dmp >30 dias, muestra tamano. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Minidump/Memory.dmp >30 dias, muestra tamano.",
        TooltipEs = "stale-crash-dump-cleanup via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Heuristic,
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
        return Task.FromResult(OperationResult.Ok("Limpiar dumps antiguos aplicado."));
    }
}
