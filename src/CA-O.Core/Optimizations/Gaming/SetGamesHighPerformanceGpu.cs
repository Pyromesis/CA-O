using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Configures Windows to prefer high-performance (dedicated) GPU for games.
/// Modifies HKCU\Software\Microsoft\DirectX\UserGpuPreferences registry settings.
/// </summary>
public sealed class SetGamesHighPerformanceGpu : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\DirectX\UserGpuPreferences",
                "DirectXUserGlobalSettings",
                "GpuPreference=2;", // 2 = High Performance (dedicated GPU)
                RegistryValueKind2.String)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "set-games-high-performance-gpu",
        NameEs = "Preferir GPU dedicada para juegos",
        NameEn = "Preferir GPU dedicada para juegos",
        DescriptionEs = "Configura Windows para preferir GPU dedicada en juegos. Beneficio potencial: mejor rendimiento en juegos con GPU dedicada disponible.",
        DescriptionEn = "Configures Windows to prefer dedicated GPU for games. Potential benefit: improved performance in games with dedicated GPU.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("GPU dedicada configurada como preferencia para juegos."));
    }
}
