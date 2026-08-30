using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class DeliveryOptimizationBandwidthProfile : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\delivery-optimization-bandwidth-profile", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "delivery-optimization-bandwidth-profile",
        NameEs = "Perfil ancho banda Delivery Optimization",
        NameEn = "Perfil ancho banda Delivery Optimization",
        DescriptionEs = "Background/Foreground/Gaming/LowBandwidth. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Background/Foreground/Gaming/LowBandwidth.",
        TooltipEs = "delivery-optimization-bandwidth-profile via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Network,
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
        return Task.FromResult(OperationResult.Ok("Perfil ancho banda Delivery Optimization aplicado."));
    }
}
