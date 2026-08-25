using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>#4: Game Bar + Game DVR capture off (two values, both snapshotted).</summary>
public sealed class DisableGameBarDvr : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } = new[]
    {
        new ValueTarget(RegistryHive2.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0),
        new ValueTarget(RegistryHive2.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
    };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar Game Bar y Game DVR",
        NameEn = "Disable Game Bar and Game DVR",
        DescriptionEs = "Apaga la barra de juegos y la grabación en segundo plano que consume CPU/GPU.",
        DescriptionEn = "Turns off the game bar and the background recording that uses CPU/GPU.",
        TooltipEs = "GameDVR_Enabled=0 y AppCaptureEnabled=0. Pierdes Win+G y los clips automáticos; gana estabilidad de frametime.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.NoKnownConflict,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.NoKnownConflict,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.High,
    };

    private static string Id => "disable-game-bar-dvr";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Game Bar y grabación de fondo desactivados."));
    }
}
