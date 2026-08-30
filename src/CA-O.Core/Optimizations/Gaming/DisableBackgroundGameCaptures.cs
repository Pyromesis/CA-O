using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

public sealed class DisableBackgroundGameCaptures : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\disable-background-game-captures", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-background-game-captures",
        NameEs = "Desactivar capturas en segundo plano",
        NameEn = "Desactivar capturas en segundo plano",
        DescriptionEs = "Separa Game DVR de capturas. Si no usa grabacion, reduce overhead. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Separa Game DVR de capturas. Si no usa grabacion, reduce overhead.",
        TooltipEs = "disable-background-game-captures via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Vendor,
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
        return Task.FromResult(OperationResult.Ok("Desactivar capturas en segundo plano aplicado."));
    }
}
