using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class DiskCleanupSystemFiles : RegistryOptimizationBase
{
    /// <summary>Enables automatic cleanup of system cache and log files via Windows Update service.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable automatic cleanup of Windows Update cache
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\WuAuServ",
                "CleanupInterval",
                7,
                RegistryValueKind2.DWord),
            // Set cleanup behavior for old update files
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\wuauserv",
                "NoAutoRebootWithLoggedOnUsers",
                0,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disk-cleanup-system-files",
        NameEs = "Limpieza de archivos del sistema",
        NameEn = "Disk cleanup system files",
        DescriptionEs = "Habilita limpieza automática de caché del sistema y archivos de registro.",
        DescriptionEn = "Enables automatic cleanup of system cache and log files.",
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
        return Task.FromResult(OperationResult.Ok("Limpieza de archivos del sistema habilitada."));
    }
}
