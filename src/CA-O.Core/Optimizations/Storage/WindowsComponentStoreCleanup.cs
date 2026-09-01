using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class WindowsComponentStoreCleanup : RegistryOptimizationBase
{
    /// <summary>Enables automatic cleanup of old Windows component store files to save disk space.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable component store cleanup (old superseded packages)
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\TrustedInstaller\Parameters",
                "AllowCleanupSupersededComponents",
                1,
                RegistryValueKind2.DWord),
            // Set aggressive cleanup for unused components
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\WinDefend\Parameters",
                "LastCleanupTime",
                0,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "windows-component-store-cleanup",
        NameEs = "Limpieza Component Store",
        NameEn = "Windows Component Store cleanup",
        DescriptionEs = "Habilita limpieza automática de archivos antiguos en el Component Store para liberar espacio.",
        DescriptionEn = "Enables automatic cleanup of old component store files to free disk space.",
        TooltipEs = "Modifica HKLM\\SYSTEM\\CurrentControlSet. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Limpieza Component Store habilitada."));
    }
}
