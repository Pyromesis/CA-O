using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Performance;

/// <summary>#3: VisualFXSetting=2 (best performance).</summary>
public sealed class DisableVisualEffects : RegistryOptimizationBase
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";

    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, KeyPath, "VisualFXSetting", 2) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar efectos visuales",
        NameEn = "Disable visual effects",
        DescriptionEs = "Prioriza rendimiento sobre apariencia en la configuración de efectos de Windows.",
        DescriptionEn = "Prefers performance over appearance in Windows effects settings.",
        TooltipEs = "Cambia VisualFXSetting a 2 (mejor rendimiento). Las animaciones y sombras se reducen. Reversible desde Restauración.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Empirical,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    private static string Id => "disable-visual-effects";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Efectos visuales configurados para mejor rendimiento."));
    }
}
