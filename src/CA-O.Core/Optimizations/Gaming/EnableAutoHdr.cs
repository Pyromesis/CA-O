using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

public sealed class EnableAutoHdr : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\enable-auto-hdr", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-auto-hdr",
        NameEs = "Activar Auto HDR",
        NameEn = "Activar Auto HDR",
        DescriptionEs = "Solo si display HDR compatible, sin impacto en rendimiento. Mejora visual.",
        DescriptionEn = "Only if HDR display compatible, no performance impact. Visual improvement.",
        TooltipEs = "enable-auto-hdr via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.None,
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
        return Task.FromResult(OperationResult.Ok("Activar Auto HDR aplicado."));
    }
}
