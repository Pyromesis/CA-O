using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// MMCSS SystemResponsiveness 20→10: menos CPU reservada a tareas de fondo,
/// más para el juego en foco. Reversible (restaura el 20).
/// </summary>
public sealed class MmcssSystemResponsiveness : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        new(RegistryHive2.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 10, RegistryValueKind2.DWord),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "mmcss-system-responsiveness",
        NameEs = "Prioridad MMCSS al juego (10)",
        NameEn = "MMCSS priority to game (10)",
        DescriptionEs = "Reserva menos CPU a tareas de fondo (10 en vez de 20).",
        DescriptionEn = "Reserves less CPU for background tasks (10 instead of 20).",
        TooltipEs = "El juego en foco recibe más CPU. Si el audio en segundo plano se entrecorta, revierte.",
        Category = OptimizationCategory.Gaming,
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
        return Task.FromResult(OperationResult.Ok("MMCSS SystemResponsiveness en 10."));
    }
}
