using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>
/// Sets wireless network adapter to maximum performance mode on AC power.
/// Reduces WiFi latency and increases throughput for online gaming.
/// Modifies HKLM registry for network adapter power configuration.
/// </summary>
public sealed class SetWirelessAdapterMaxPerformanceAc : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\NETwCx\Parameters",
                "PowerSavingMode",
                0, // 0 = Maximum Performance, 1 = Power Saving, 2 = Balanced
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "set-wireless-adapter-max-performance-ac",
        NameEs = "Adaptador WiFi a máximo rendimiento en AC",
        NameEn = "Wireless adapter maximum performance on AC",
        DescriptionEs = "Configura el adaptador WiFi a máximo rendimiento en AC. Reduce latencia WiFi e incrementa throughput para gaming en línea.",
        DescriptionEn = "Sets wireless adapter to maximum performance on AC power. Reduces WiFi latency and increases throughput for online gaming.",
        TooltipEs = "Modifica HKLM\\SYSTEM\\CurrentControlSet\\Services\\NETwCx\\Parameters. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
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
        return Task.FromResult(OperationResult.Ok("Adaptador WiFi configurado a máximo rendimiento en AC."));
    }
}
