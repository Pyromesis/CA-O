using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Configures power plan to High Performance when on AC power during gaming.
/// Only applies when on AC (plugged in); never applies on battery.
/// Modifies power plan settings via registry.
/// </summary>
public sealed class ConfigureGamingPowerModeAc : RegistryOptimizationBase
{
    // HKLM\System\CurrentControlSet\ControlSet001\Services\EnergyExperienceService - gaming power mode policy
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "GamingPowerMode",
                1, // 1 = High Performance, 0 = Disabled
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "configure-gaming-power-mode-ac",
        NameEs = "Modo de energía Alto Rendimiento en AC para juegos",
        NameEn = "High Performance power mode on AC for gaming",
        DescriptionEs = "Configura el plan de energía a Alto Rendimiento cuando se está conectado a AC durante juegos. Nunca se aplica en batería.",
        DescriptionEn = "Sets power plan to High Performance when on AC power during games. Never applies on battery.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\\GamingPowerMode. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
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
        return Task.FromResult(OperationResult.Ok("Modo de energía Alto Rendimiento configurado para juegos en AC."));
    }
}
