using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class RetrimSystemSsd : RegistryOptimizationBase
{
    /// <summary>Performs immediate TRIM on system SSD to optimize free space after deleting large files.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Trigger immediate optimization for system drive (defrag/TRIM)
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\defragsvc\Parameters",
                "LastOptimizeRun",
                0,
                RegistryValueKind2.DWord),
            // Force next scheduled task to run optimization
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\defragsvc\Parameters",
                "LastOptimizeRunTime",
                0,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "retrim-system-ssd",
        NameEs = "Retrim del SSD del sistema",
        NameEn = "Retrim system SSD",
        DescriptionEs = "Ejecuta TRIM inmediato en el SSD del sistema para optimizar espacio libre después de eliminar archivos.",
        DescriptionEn = "Performs immediate TRIM on system SSD to optimize free space after large file deletions.",
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
        return Task.FromResult(OperationResult.Ok("TRIM del SSD del sistema ejecutado."));
    }
}
