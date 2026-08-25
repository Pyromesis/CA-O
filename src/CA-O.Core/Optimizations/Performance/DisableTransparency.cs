using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Performance;

/// <summary>#12: EnableTransparency=0.</summary>
public sealed class DisableTransparency : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar transparencias",
        NameEn = "Disable transparency",
        DescriptionEs = "Desactiva acrílico/transparencias del sistema para ahorrar composición GPU.",
        DescriptionEn = "Turns off system acrylic/transparency effects to save GPU composition work.",
        TooltipEs = "EnableTransparency=0. Nota: también apaga el estilo visual Fluent translúcido (preferencia estética).",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    private static string Id => "disable-transparency";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Transparencias desactivadas."));
    }
}
