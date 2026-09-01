using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Disables automatic Game Bar launch when Alt+Z is pressed (gaming scenarios).
/// Modifies HKCU\Software\Microsoft\GameBar registry settings.
/// </summary>
public sealed class DisableGameBarAutoLaunch : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\GameBar",
                "AllowAutoGameMode",
                0, // 0 = Disabled, 1 = Enabled
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-game-bar-auto-launch",
        NameEs = "Deshabilitar lanzamiento automático de Game Bar",
        NameEn = "Disable automatic Game Bar launch",
        DescriptionEs = "Evita el lanzamiento automático de Game Bar cuando se presiona Alt+Z en juegos. La funcionalidad sigue disponible manualmente.",
        DescriptionEn = "Prevents automatic Game Bar launch on Alt+Z in games. Functionality remains available manually.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\GameBar\\AllowAutoGameMode. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Lanzamiento automático de Game Bar deshabilitado."));
    }
}
