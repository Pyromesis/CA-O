using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class RestoreWindowsTcpCongestionDefault : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-windows-tcp-congestion-default", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-windows-tcp-congestion-default",
        NameEs = "Restaurar congestion TCP por defecto",
        NameEn = "Restaurar congestion TCP por defecto",
        DescriptionEs = "Detecta provider no estandar y restaura estandar. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Detecta provider no estandar y restaura estandar.",
        TooltipEs = "restore-windows-tcp-congestion-default via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Network,
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
        return Task.FromResult(OperationResult.Ok("Restaurar congestion TCP por defecto aplicado."));
    }
}
