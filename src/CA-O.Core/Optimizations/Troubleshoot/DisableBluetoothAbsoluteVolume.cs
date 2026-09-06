using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Disables Bluetooth absolute volume (fixes volume control on BT headsets).</summary>
public sealed class DisableBluetoothAbsoluteVolume : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.LocalMachine, @"SYSTEM\ControlSet001\Control\Bluetooth\Audio\AVRCP\CT", "DisableAbsoluteVolume", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-bluetooth-absolute-volume",
        NameEs = "Desactivar volumen absoluto Bluetooth",
        NameEn = "Disable Bluetooth absolute volume",
        DescriptionEs = "Devuelve el control de volumen independiente en auriculares Bluetooth.",
        DescriptionEn = "Restores independent volume control on Bluetooth headsets.",
        TooltipEs = "Establece DisableAbsoluteVolume=1 en AVRCP. Reversible exacto.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Volumen absoluto Bluetooth desactivado."));
    }
}
