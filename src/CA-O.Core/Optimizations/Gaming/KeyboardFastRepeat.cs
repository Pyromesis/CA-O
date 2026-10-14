using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Retraso de repetición mínimo (KeyboardDelay=0, ~250 ms) y velocidad
/// máxima (KeyboardSpeed=31, ~30 repeticiones/s): los mismos extremos que
/// ofrece el panel de Teclado de Windows (SPI_SETKEYBOARDDELAY/SPEED). Una
/// tecla mantenida responde antes. Reversible.
/// </summary>
public sealed class KeyboardFastRepeat : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        new(RegistryHive2.CurrentUser, @"Control Panel\Keyboard", "KeyboardDelay", "0", RegistryValueKind2.String),
        new(RegistryHive2.CurrentUser, @"Control Panel\Keyboard", "KeyboardSpeed", "31", RegistryValueKind2.String),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "keyboard-fast-repeat",
        NameEs = "Teclado: repetición más rápida",
        NameEn = "Keyboard: faster repeat",
        DescriptionEs = "Menor retraso y mayor velocidad de repetición al mantener una tecla.",
        DescriptionEn = "Shorter delay and faster repeat rate when holding a key.",
        TooltipEs = "Los mismos extremos del panel Teclado de Windows (retraso corto, velocidad máxima). Se aplica al volver a iniciar sesión. Reversible.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Tiny,
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
        return Task.FromResult(OperationResult.Ok("Repetición de teclado al máximo (efecto al iniciar sesión)."));
    }
}
