using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Precisión de puntero OFF 1:1 (equivale a desmarcar "Mejorar precisión del
/// puntero"): MouseSpeed/Thresholds a 0. Sin aceleración variable, la memoria
/// muscular es fiable en shooters. Reversible.
/// </summary>
public sealed class DisablePointerPrecision : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        new(RegistryHive2.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind2.String),
        new(RegistryHive2.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind2.String),
        new(RegistryHive2.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind2.String),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-pointer-precision",
        NameEs = "Ratón 1:1 sin aceleración",
        NameEn = "1:1 mouse, no acceleration",
        DescriptionEs = "Desactiva la aceleración del puntero (movimiento 1:1 fiable).",
        DescriptionEn = "Disables pointer acceleration (reliable 1:1 movement).",
        TooltipEs = "Equivale a desmarcar 'Mejorar precisión del puntero' en Windows. Sin esto, Windows acelera el cursor según la velocidad y rompe la puntería.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Aceleración de puntero desactivada (1:1)."));
    }
}
