using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

public sealed class ConfigureGamingPowerModeAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\configure-gaming-power-mode-ac", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "configure-gaming-power-mode-ac",
        NameEs = "Modo energia Best Performance en AC",
        NameEn = "Modo energia Best Performance en AC",
        DescriptionEs = "AC -> Best Performance solo para Gaming/Competitive, nunca en bateria. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "AC -> Best Performance solo para Gaming/Competitive, nunca en bateria.",
        TooltipEs = "configure-gaming-power-mode-ac via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
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
        return Task.FromResult(OperationResult.Ok("Modo energia Best Performance en AC aplicado."));
    }
}
