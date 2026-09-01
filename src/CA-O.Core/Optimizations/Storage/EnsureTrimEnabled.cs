using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class EnsureTrimEnabled : RegistryOptimizationBase
{
    /// <summary>TRIM ensures SSD performance by enabling garbage collection on deleted blocks.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable Scheduled defragmentation and optimization (includes TRIM for SSDs)
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\defragsvc\Parameters",
                "EnableScheduledMaintenance",
                1,
                RegistryValueKind2.DWord),
            // Set optimization run interval (weekly)
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\defragsvc\Parameters",
                "OptimizeInterval",
                7,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "ensure-trim-enabled",
        NameEs = "Asegurar TRIM habilitado en SSD",
        NameEn = "Ensure TRIM enabled on SSD",
        DescriptionEs = "Habilita TRIM en SSD para mantener rendimiento mediante liberación de bloques eliminados.",
        DescriptionEn = "Enables TRIM on SSD to maintain performance by freeing deleted blocks.",
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
        return Task.FromResult(OperationResult.Ok("TRIM habilitado en SSD."));
    }
}
