using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

public sealed class DisableGameBarAutoLaunch : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\disable-game-bar-auto-launch", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-game-bar-auto-launch",
        NameEs = "Evitar inicio automatico de Game Bar",
        NameEn = "Evitar inicio automatico de Game Bar",
        DescriptionEs = "Evita lanzamiento automatico sin eliminar funcionalidad. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Evita lanzamiento automatico sin eliminar funcionalidad.",
        TooltipEs = "disable-game-bar-auto-launch via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Evitar inicio automatico de Game Bar aplicado."));
    }
}
