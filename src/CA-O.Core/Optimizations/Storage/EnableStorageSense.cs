using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Enables Storage Sense feature on Windows 10+.
/// Automatically deletes temporary files when storage runs low.
/// Official Windows configuration in HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense.
/// </summary>
public sealed class EnableStorageSense : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable Storage Sense (version 3 on Win10+)
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicies",
                "01",
                1,
                RegistryValueKind2.DWord),
            // Set cleanup frequency to daily
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicies",
                "02",
                0, // 0 = Every day
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-storage-sense",
        NameEs = "Activar Storage Sense",
        NameEn = "Enable Storage Sense",
        DescriptionEs = "Activa Storage Sense para limpiar automáticamente archivos temporales cuando el espacio es bajo.",
        DescriptionEn = "Enables automatic cleanup of temporary files when storage runs low.",
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
        return Task.FromResult(OperationResult.Ok("Storage Sense habilitado."));
    }
}
