using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

public sealed class GamingDisplayRefreshRateAudit : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\gaming-display-refresh-rate-audit", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "gaming-display-refresh-rate-audit",
        NameEs = "Auditoria de frecuencia de pantalla",
        NameEn = "Auditoria de frecuencia de pantalla",
        DescriptionEs = "Detecta Hz max vs actual, no modifica a ciegas. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Detecta Hz max vs actual, no modifica a ciegas.",
        TooltipEs = "gaming-display-refresh-rate-audit via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Auditoria de frecuencia de pantalla aplicado."));
    }
}
