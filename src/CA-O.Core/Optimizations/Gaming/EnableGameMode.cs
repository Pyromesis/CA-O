using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>1. enable-game-mode — Game Mode on when disabled.</summary>
public sealed class EnableGameMode : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-game-mode",
        NameEs = "Activar Modo Juego",
        NameEn = "Enable Game Mode",
        DescriptionEs = "Activa el Modo Juego de Windows cuando está desactivado. Prioriza juegos reduciendo interferencia de fondo.",
        DescriptionEn = "Enables Windows Game Mode when disabled. Prioritizes games by reducing background interference.",
        TooltipEs = "AllowAutoGameMode=1. Requiere reinicio de juego. No fuerza si política lo bloquea.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Modo Juego activado."));
    }
}
