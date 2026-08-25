using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Performance;

/// <summary>#10: MenuShowDelay=0 (instant menus; comfort tweak).</summary>
public sealed class ZeroMenuDelay : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay", "0", RegistryValueKind2.String) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Apertura instantánea de menús",
        NameEn = "Instant menu open",
        DescriptionEs = "Elimina el retardo de 400 ms al abrir menús contextuales.",
        DescriptionEn = "Removes the 400 ms delay before context menus open.",
        TooltipEs = "MenuShowDelay=0. Es una preferencia de respuesta de UI, no mejora el rendimiento en juegos.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Heuristic,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    private static string Id => "zero-menu-delay";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Menús instantáneos."));
    }
}
