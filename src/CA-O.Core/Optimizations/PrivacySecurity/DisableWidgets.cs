using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.PrivacySecurity;

/// <summary>#16: AllowNewsAndInterests=0 (Widgets policy).</summary>
public sealed class DisableWidgets : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar Widgets",
        NameEn = "Disable Widgets",
        DescriptionEs = "Desactiva el panel de Widgets y sus contenidos en segundo plano.",
        DescriptionEn = "Turns off the Widgets panel and its background content.",
        TooltipEs = "Directiva Dsh\\AllowNewsAndInterests=0. Elimina procesos de feeds en segundo plano.",
        Category = OptimizationCategory.PrivacySecurity,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.NoKnownConflict,
        SecurityImpact = SecurityImpact.PrivacyOnly,
        Impact = ImpactLevel.Low,
    };

    private static string Id => "disable-widgets";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Widgets desactivados."));
    }
}
