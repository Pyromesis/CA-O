using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Win32PrioritySeparation=0x26 (38): quantums cortos y variables con
/// impulso 3:1 al proceso en primer plano, la opción "Programas" de
/// Opciones de rendimiento. Algunos instaladores, servidores y "tweakers"
/// dejan "Servicios en segundo plano" (0x18) o valores raros, y entonces el
/// juego con foco compite a partes iguales con todo lo demás. Reversible.
/// </summary>
public sealed class EnsureForegroundPrioritySeparation : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        new(RegistryHive2.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26, RegistryValueKind2.DWord),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "ensure-foreground-priority-separation",
        NameEs = "Prioridad al programa en primer plano",
        NameEn = "Priority to the foreground program",
        DescriptionEs = "Asegura que la ventana con foco (el juego) recibe más turnos de CPU que lo de fondo.",
        DescriptionEn = "Ensures the focused window (the game) gets more CPU turns than background work.",
        TooltipEs = "Win32PrioritySeparation = 0x26: la opción 'Programas' de Opciones de rendimiento, con quantums cortos e impulso al primer plano. Corrige equipos configurados como servidor. Reversible.",
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
        return Task.FromResult(OperationResult.Ok("Prioridad de primer plano asegurada (0x26)."));
    }
}
