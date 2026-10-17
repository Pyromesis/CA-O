using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// DOPercentageMaxBackgroundBandwidth=20: las descargas de Windows Update y
/// de la Store en segundo plano usan como mucho una quinta parte del ancho de
/// banda medido. Sin límite pueden llenar el enlace y la cola del router
/// (bufferbloat), y el ping del juego se dispara aunque no estés descargando
/// nada tú. Las descargas en primer plano no se limitan. Política oficial de
/// Delivery Optimization. Reversible.
/// </summary>
public sealed class LimitUpdateBackgroundBandwidth : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        new(RegistryHive2.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DOPercentageMaxBackgroundBandwidth", 20, RegistryValueKind2.DWord),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "limit-update-background-bandwidth",
        NameEs = "Windows Update no satura tu conexión",
        NameEn = "Windows Update does not saturate your link",
        DescriptionEs = "Las descargas de actualizaciones en segundo plano usan como mucho una quinta parte de tu ancho de banda.",
        DescriptionEn = "Background update downloads use at most a fifth of your bandwidth.",
        TooltipEs = "Política oficial DOPercentageMaxBackgroundBandwidth. Evita picos de ping por descargas que no lanzaste tú. Las actualizaciones tardan algo más en bajar. Reversible.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Descargas de fondo de Windows Update limitadas."));
    }
}
