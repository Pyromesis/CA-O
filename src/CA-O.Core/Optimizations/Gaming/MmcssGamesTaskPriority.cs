using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Tarea MMCSS "Games": prioridad 6 y categoría High (Windows trae 2 y
/// Medium). Solo afecta a los hilos que un juego registra en MMCSS (motores
/// que usan AvSetMmThreadCharacteristics "Games"); al resto no les cambia
/// nada. Complementa a mmcss-system-responsiveness. Reversible.
/// </summary>
public sealed class MmcssGamesTaskPriority : RegistryOptimizationBase
{
    private const string GamesTask = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        new(RegistryHive2.LocalMachine, GamesTask, "GPU Priority", 8, RegistryValueKind2.DWord),
        new(RegistryHive2.LocalMachine, GamesTask, "Priority", 6, RegistryValueKind2.DWord),
        new(RegistryHive2.LocalMachine, GamesTask, "Scheduling Category", "High", RegistryValueKind2.String),
        new(RegistryHive2.LocalMachine, GamesTask, "SFIO Priority", "High", RegistryValueKind2.String),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "mmcss-games-task-priority",
        NameEs = "MMCSS: prioridad alta para hilos de juego",
        NameEn = "MMCSS: high priority for game threads",
        DescriptionEs = "Sube la prioridad de la tarea Games de MMCSS (6, categoría High).",
        DescriptionEn = "Raises the MMCSS Games task priority (6, High category).",
        TooltipEs = "Solo afecta a juegos que registran sus hilos en MMCSS; el resto no cambia. Si el audio de fondo se entrecorta, revierte. Reversible.",
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
        return Task.FromResult(OperationResult.Ok("Tarea MMCSS Games con prioridad alta."));
    }
}
