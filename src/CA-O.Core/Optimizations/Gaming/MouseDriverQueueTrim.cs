using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Recorta la cola del driver de ratón (mouclass MouseDataQueueSize 100→32):
/// menos buffering de paquetes del sensor antes de procesarse. Requiere
/// reinicio. En ratones muy antiguos podría perder eventos: reversible.
/// </summary>
public sealed class MouseDriverQueueTrim : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        new(RegistryHive2.LocalMachine, @"SYSTEM\CurrentControlSet\Services\mouclass\Parameters", "MouseDataQueueSize", 32, RegistryValueKind2.DWord),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "mouse-driver-queue-trim",
        NameEs = "Cola de ratón recortada (32)",
        NameEn = "Trimmed mouse queue (32)",
        DescriptionEs = "Baja el buffer del driver de ratón de 100 a 32 paquetes.",
        DescriptionEn = "Lowers the mouse driver queue from 100 to 32 packets.",
        TooltipEs = "Medido por la comunidad: menos input lag con sensores modernos. Requiere reinicio. Reversible.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.RequiresReboot,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Cola de ratón en 32. Reinicia para aplicar."));
    }
}
