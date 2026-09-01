using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Enables Auto HDR feature in Windows to automatically convert SDR games to HDR.
/// Only works on HDR-capable displays. No performance impact; visual enhancement only.
/// Modifies HKCU\Software\Microsoft\GameBar registry settings.
/// </summary>
public sealed class EnableAutoHdr : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\GameBar",
                "AutoHdrToggleState",
                1, // 1 = Enabled, 0 = Disabled
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-auto-hdr",
        NameEs = "Activar Auto HDR automático",
        NameEn = "Enable automatic Auto HDR",
        DescriptionEs = "Activa Auto HDR en Windows para convertir automáticamente juegos SDR a HDR. Solo funciona en pantallas compatibles con HDR. Sin impacto en rendimiento; solo mejora visual.",
        DescriptionEn = "Enables Auto HDR in Windows to automatically convert SDR games to HDR. Only works on HDR-capable displays. No performance impact; visual enhancement only.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\GameBar\\AutoHdrToggleState. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Auto HDR automático habilitado."));
    }
}
