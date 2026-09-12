using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Disables Game DVR background recording (historical capture) switches.
/// </summary>
public sealed class DisableBackgroundGameCaptures : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\GameDVR",
                "HistoricalCaptureEnabled",
                0,
                RegistryValueKind2.DWord),
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\GameDVR",
                "HistoricalCaptureOnBattery",
                0,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-background-game-captures",
        NameEs = "Desactivar capturas en segundo plano de Game DVR",
        NameEn = "Disable background Game DVR captures",
        DescriptionEs = "Deshabilita la grabación histórica en segundo plano de Game DVR. Reduce overhead si no graba.",
        DescriptionEn = "Disables Game DVR background historical recording. Reduces overhead when not recording.",
        TooltipEs = "Establece HistoricalCaptureEnabled=0 e HistoricalCaptureOnBattery=0. Reversible via snapshot. Si ya desactivaste Game Bar DVR por completo, esta sobra.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Capturas en segundo plano de Game DVR deshabilitadas."));
    }
}
