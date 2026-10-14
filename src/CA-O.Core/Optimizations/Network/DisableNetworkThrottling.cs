using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// NetworkThrottlingIndex=0xFFFFFFFF: mientras suena audio/vídeo (MMCSS),
/// Windows limita por defecto el procesado de paquetes que no son multimedia
/// a 10 por milisegundo. Con voz o música de fondo eso también frena el
/// tráfico del juego. 0xFFFFFFFF desactiva el límite. Reversible.
/// </summary>
public sealed class DisableNetworkThrottling : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        new(RegistryHive2.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
            "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind2.DWord),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-network-throttling",
        NameEs = "Red sin límite con audio de fondo",
        NameEn = "No network throttling with background audio",
        DescriptionEs = "Quita el límite de paquetes que Windows aplica mientras suena audio (Discord, música).",
        DescriptionEn = "Removes the packet cap Windows applies while audio plays (Discord, music).",
        TooltipEs = "NetworkThrottlingIndex sin límite. Útil si juegas con voz o música de fondo. Algo más de CPU en descargas grandes. Reversible.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
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
        return Task.FromResult(OperationResult.Ok("Límite de red multimedia desactivado."));
    }
}
