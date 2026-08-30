using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

public sealed class EnableWindowedGameOptimizations : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\enable-windowed-game-optimizations", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-windowed-game-optimizations",
        NameEs = "Activar optimizaciones para juegos en ventana",
        NameEn = "Activar optimizaciones para juegos en ventana",
        DescriptionEs = "Habilita optimizaciones para juegos DX10/11 en ventana/borderless. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Habilita optimizaciones para juegos DX10/11 en ventana/borderless.",
        TooltipEs = "enable-windowed-game-optimizations via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
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
        return Task.FromResult(OperationResult.Ok("Activar optimizaciones para juegos en ventana aplicado."));
    }
}
