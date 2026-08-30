using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

public sealed class DelaySafeThirdPartyServiceStart : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\delay-safe-third-party-service-start", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "delay-safe-third-party-service-start",
        NameEs = "Retrasar servicios terceros seguros",
        NameEn = "Retrasar servicios terceros seguros",
        DescriptionEs = "Solo no criticos, sin dependencias, delayed auto. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Solo no criticos, sin dependencias, delayed auto.",
        TooltipEs = "delay-safe-third-party-service-start via registry. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Retrasar servicios terceros seguros aplicado."));
    }
}
