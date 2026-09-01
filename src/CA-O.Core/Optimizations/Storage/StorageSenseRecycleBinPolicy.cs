using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class StorageSenseRecycleBinPolicy : RegistryOptimizationBase
{
    /// <summary>Configures Storage Sense to automatically empty Recycle Bin after 30 days.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable Storage Sense cleanup of Recycle Bin
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\\Microsoft\\Windows\\CurrentVersion\\StorageSense\\Parameters\\StoragePolicies",
                "04",
                1,
                RegistryValueKind2.DWord),
            // Set Recycle Bin retention to 30 days
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\\Microsoft\\Windows\\CurrentVersion\\StorageSense\\Parameters\\StoragePolicies",
                "05",
                30,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "storage-sense-recycle-bin-policy",
        NameEs = "Política Storage Sense Papelera",
        NameEn = "Storage Sense Recycle Bin policy",
        DescriptionEs = "Configura Storage Sense para vaciar automáticamente la Papelera de Reciclaje después de 30 días.",
        DescriptionEn = "Configures Storage Sense to automatically empty Recycle Bin after 30 days.",
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
        return Task.FromResult(OperationResult.Ok("Política Storage Sense Papelera aplicada."));
    }
}
