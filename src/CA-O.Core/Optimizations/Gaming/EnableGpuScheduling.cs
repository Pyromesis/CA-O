using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>#11: HwSchMode=2 (Hardware-accelerated GPU scheduling). Reboot required.</summary>
public sealed class EnableGpuScheduling : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Programación de GPU acelerada por hardware",
        NameEn = "Hardware-accelerated GPU scheduling",
        DescriptionEs = "Activa HAGS. Requisito para DLSS Frame Generation; el efecto varía por título.",
        DescriptionEn = "Enables HAGS. Required for DLSS Frame Generation; effect varies per title.",
        TooltipEs = "HwSchMode=2 + reinicio. En GPUs antiguas puede no estar soportado o no aportar; reversible.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Vendor,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
        Flags = OptimizationFlags.RequiresReboot,
    };

    private static string Id => "enable-gpu-scheduling";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("HAGS activado (aplica tras reiniciar)."));
    }
}
