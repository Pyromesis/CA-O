using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>Activates the High Performance power plan on AC power.</summary>
public sealed class SetBestPerformanceAc : PowerSchemeSwitchOptimization
{
    protected override string TargetScheme => "SCHEME_MAX";
    protected override string TargetLabel => "Alto rendimiento";

    public override OptimizationDefinition Definition => new()
    {
        Id = "set-best-performance-ac",
        NameEs = "Modo Alto Rendimiento en AC",
        NameEn = "High Performance mode on AC power",
        DescriptionEs = "Activa el plan Alto rendimiento con powercfg. Solo aplica cuando está conectado.",
        DescriptionEn = "Activates the High performance plan via powercfg. Only applies when plugged in.",
        TooltipEs = "Ejecuta powercfg /setactive SCHEME_MAX y restaura el previo al revertir.",
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

    public override Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(context.OnBattery
            ? PreconditionResult.Fail("Solo con alimentación AC.")
            : PreconditionResult.Ok("Con alimentación AC."));
}
