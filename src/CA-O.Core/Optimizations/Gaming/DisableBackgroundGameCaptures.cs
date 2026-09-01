using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Disables background Game DVR captures to reduce memory/CPU overhead.
/// Modifies HKCU\Software\Microsoft\GameBar registry settings.
/// </summary>
public sealed class DisableBackgroundGameCaptures : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\GameBar",
                "UseNativeRuntime",
                0, // 0 = Disabled, 1 = Enabled
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-background-game-captures",
        NameEs = "Desactivar capturas en segundo plano de Game DVR",
        NameEn = "Disable background Game DVR captures",
        DescriptionEs = "Deshabilita capturas automáticas en segundo plano de Game DVR. Reduce overhead de memoria y CPU si no usa grabación.",
        DescriptionEn = "Disables automatic background Game DVR captures. Reduces memory and CPU overhead if not recording.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\GameBar\\UseNativeRuntime. Reversible via snapshot.",
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
