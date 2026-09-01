using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>
/// Disables USB Selective Suspend on AC power to prevent USB devices from suspending.
/// Critical for gaming mice, keyboards, and controllers on AC power.
/// Modifies HKLM registry for USB hub configuration.
/// </summary>
public sealed class DisableUsbSelectiveSuspendAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\usbhub\Parameters",
                "DisableSelectiveSuspend",
                1, // 1 = Disabled, 0 = Enabled
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-usb-selective-suspend-ac",
        NameEs = "Deshabilitar suspensión selectiva USB en AC",
        NameEn = "Disable USB Selective Suspend on AC",
        DescriptionEs = "Deshabilita la suspensión selectiva de USB cuando está en AC. Previene que ratones, teclados y controles se suspendan durante el juego.",
        DescriptionEn = "Disables USB Selective Suspend on AC power. Prevents gaming mice, keyboards, and controllers from suspending.",
        TooltipEs = "Modifica HKLM\\SYSTEM\\CurrentControlSet\\Services\\usbhub\\Parameters. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Suspensión selectiva USB deshabilitada en AC."));
    }
}
