using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>Limits Delivery Optimization to LAN peers (DODownloadMode=1).</summary>
public sealed class DeliveryOptimizationBandwidthProfile : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "delivery-optimization-bandwidth-profile",
        NameEs = "Delivery Optimization solo en LAN",
        NameEn = "Delivery Optimization LAN only",
        DescriptionEs = "Limita Delivery Optimization a equipos de la red local. Reduce el uso de internet en segundo plano.",
        DescriptionEn = "Limits Delivery Optimization to LAN peers. Reduces background internet usage.",
        TooltipEs = "Establece HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\\DODownloadMode=1. Reversible exacto.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Delivery Optimization limitado a LAN."));
    }
}
