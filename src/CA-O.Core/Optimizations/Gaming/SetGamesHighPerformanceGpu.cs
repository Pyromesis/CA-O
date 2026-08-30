using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

public sealed class SetGamesHighPerformanceGpu : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\set-games-high-performance-gpu", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "set-games-high-performance-gpu",
        NameEs = "GPU alto rendimiento por juego",
        NameEn = "GPU alto rendimiento por juego",
        DescriptionEs = "Asigna GPU dedicada via preferencias graficas de Windows. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Asigna GPU dedicada via preferencias graficas de Windows.",
        TooltipEs = "set-games-high-performance-gpu via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("GPU alto rendimiento por juego aplicado."));
    }
}
