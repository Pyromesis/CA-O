using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

public sealed class EnableVrr : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\enable-vrr", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-vrr",
        NameEs = "Activar Variable Refresh Rate",
        NameEn = "Activar Variable Refresh Rate",
        DescriptionEs = "Gestiona VRR solo si monitor, driver y GPU lo soportan. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Gestiona VRR solo si monitor, driver y GPU lo soportan.",
        TooltipEs = "enable-vrr via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Activar Variable Refresh Rate aplicado."));
    }
}
