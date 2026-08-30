using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

public sealed class RestoreWindowsSearchDefault : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\restore-windows-search-default", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-windows-search-default",
        NameEs = "Restaurar Windows Search por defecto",
        NameEn = "Restaurar Windows Search por defecto",
        DescriptionEs = "Corrige si Search deshabilitado, Search reduce actividad segun carga. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Corrige si Search deshabilitado, Search reduce actividad segun carga.",
        TooltipEs = "restore-windows-search-default via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
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
        return Task.FromResult(OperationResult.Ok("Restaurar Windows Search por defecto aplicado."));
    }
}
