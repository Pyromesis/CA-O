using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>
/// Disables PCIe Link State Power Management on AC power.
/// Prevents GPUs and PCIe devices from reducing speed during gaming.
/// Modifies HKLM registry for PCI controller configuration.
/// </summary>
public sealed class DisablePcieLinkStatePowerSavingAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\pci\Parameters",
                "DisableLinkStateThrottling",
                1, // 1 = Disabled throttling, 0 = Enabled
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-pcie-link-state-power-saving-ac",
        NameEs = "Deshabilitar ahorro de energía en PCIe en AC",
        NameEn = "Disable PCIe Link State Power Saving on AC",
        DescriptionEs = "Deshabilita la gestión de energía del estado de enlace PCIe en AC. Previene que GPUs y dispositivos PCIe reduzcan velocidad durante gaming.",
        DescriptionEn = "Disables PCIe Link State Power Management on AC. Prevents GPUs and PCIe devices from reducing speed during gaming.",
        TooltipEs = "Modifica HKLM\\SYSTEM\\CurrentControlSet\\Services\\pci\\Parameters. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
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
        return Task.FromResult(OperationResult.Ok("Gestión de energía PCIe deshabilitada en AC."));
    }
}
