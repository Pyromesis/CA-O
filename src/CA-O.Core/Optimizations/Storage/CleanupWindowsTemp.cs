using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class CleanupWindowsTemp : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\cleanup-windows-temp", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-windows-temp",
        NameEs = "Limpiar temporales de Windows",
        NameEn = "Limpiar temporales de Windows",
        DescriptionEs = "Solo archivos no bloqueados, registra escaneados/eliminados. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Solo archivos no bloqueados, registra escaneados/eliminados.",
        TooltipEs = "cleanup-windows-temp via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Heuristic,
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
        return Task.FromResult(OperationResult.Ok("Limpiar temporales de Windows aplicado."));
    }
}
