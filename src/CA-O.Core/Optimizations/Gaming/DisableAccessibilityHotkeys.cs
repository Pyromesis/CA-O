using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Quita los atajos de Teclas especiales (Mayús x5), Teclas filtro (Mayús
/// derecha 8 s) y Teclas de alternancia (Bloq Num 5 s): en mitad de una
/// partida abren un diálogo que saca al juego de foco. Solo desactiva el
/// ATAJO; las funciones de accesibilidad siguen disponibles en Configuración.
/// Valores Flags documentados (STICKYKEYS/FILTERKEYS/TOGGLEKEYS). Reversible.
/// </summary>
public sealed class DisableAccessibilityHotkeys : RegistryOptimizationBase
{
    private const string Accessibility = @"Control Panel\Accessibility";

    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        // 506 = 510 (defecto) sin SKF_HOTKEYACTIVE (0x4).
        new(RegistryHive2.CurrentUser, Accessibility + @"\StickyKeys", "Flags", "506", RegistryValueKind2.String),
        // 122 = 126 (defecto) sin FKF_HOTKEYACTIVE (0x4).
        new(RegistryHive2.CurrentUser, Accessibility + @"\Keyboard Response", "Flags", "122", RegistryValueKind2.String),
        // 58 = 62 (defecto) sin TKF_HOTKEYACTIVE (0x4).
        new(RegistryHive2.CurrentUser, Accessibility + @"\ToggleKeys", "Flags", "58", RegistryValueKind2.String),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-accessibility-hotkeys",
        NameEs = "Sin atajos de Teclas especiales en juego",
        NameEn = "No sticky/filter keys hotkeys in games",
        DescriptionEs = "Evita que pulsar Mayús 5 veces abra un diálogo que te saca del juego.",
        DescriptionEn = "Stops pressing Shift 5 times from opening a dialog that pulls you out of the game.",
        TooltipEs = "Solo desactiva los atajos de Teclas especiales, filtro y alternancia; las funciones siguen en Configuración > Accesibilidad. Se aplica al volver a iniciar sesión. Reversible.",
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
        return Task.FromResult(OperationResult.Ok("Atajos de accesibilidad desactivados (efecto al iniciar sesión)."));
    }
}
