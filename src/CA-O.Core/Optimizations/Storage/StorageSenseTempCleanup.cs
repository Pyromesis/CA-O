using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class StorageSenseTempCleanup : RegistryOptimizationBase
{
    /// <summary>Configures Storage Sense to automatically clean temporary files after 30 days.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable Storage Sense cleanup of temporary files
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\\Microsoft\\Windows\\CurrentVersion\\StorageSense\\Parameters\\StoragePolicies",
                "03",
                1,
                RegistryValueKind2.DWord),
            // Set temp file retention to 30 days
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\\Microsoft\\Windows\\CurrentVersion\\StorageSense\\Parameters\\StoragePolicies",
                "06",
                30,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "storage-sense-temp-cleanup",
        NameEs = "Storage Sense Limpieza Temporal",
        NameEn = "Storage Sense temp file cleanup",
        DescriptionEs = "Configura Storage Sense para limpiar automáticamente archivos temporales después de 30 días.",
        DescriptionEn = "Configures Storage Sense to automatically clean temporary files after 30 days.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\StorageSense. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Tiny,
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
        return Task.FromResult(OperationResult.Ok("Storage Sense limpieza temporal aplicada."));
    }
}
