using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class OptimizeHddMediaAware : RegistryOptimizationBase
{
    /// <summary>Enables media-aware optimization that defrags HDDs and TRIMs SSDs appropriately.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable automatic media-aware optimization
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\defragsvc\Parameters",
                "AllowScheduledMaintenance",
                1,
                RegistryValueKind2.DWord),
            // Enable media type detection (0 = auto-detect)
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\defragsvc\Parameters",
                "ScheduledOptimizationNotification",
                1,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "optimize-hdd-media-aware",
        NameEs = "Optimizar HDD media-aware",
        NameEn = "Optimize HDD media-aware",
        DescriptionEs = "Configura optimización automática según el tipo de disco: desfragmentación en HDD y TRIM en SSD.",
        DescriptionEn = "Enables automatic media-aware optimization (defrags HDD, TRIMs SSD).",
        TooltipEs = "Modifica HKLM\\SYSTEM\\CurrentControlSet\\Services\\defragsvc. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
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
        return Task.FromResult(OperationResult.Ok("Optimización media-aware configurada."));
    }
}
