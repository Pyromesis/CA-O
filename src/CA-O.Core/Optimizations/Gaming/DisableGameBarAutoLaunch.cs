using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Disables the Xbox Game Bar overlay background activity so it does not
/// auto-launch. Uses the per-app background access key (documented Settings
/// mechanism), not the Auto Game Mode value.
/// </summary>
public sealed class DisableGameBarAutoLaunch : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications\Microsoft.XboxGamingOverlay_8wekyb3d8bbwe",
                "Disabled",
                1,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-game-bar-auto-launch",
        NameEs = "Deshabilitar lanzamiento automático de Game Bar",
        NameEn = "Disable automatic Game Bar launch",
        DescriptionEs = "Impide que Game Bar se ejecute en segundo plano y se lance sola. Sigue disponible manualmente.",
        DescriptionEn = "Stops Game Bar from running in the background and auto-launching. Still available manually.",
        TooltipEs = "Establece Disabled=1 en la clave de segundo plano de XboxGamingOverlay. Reversible via snapshot.",
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
