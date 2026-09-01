using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class CleanupWindowsTemp : RegistryOptimizationBase
{
    /// <summary>Configures automatic cleanup of Windows temporary files via registry policies.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable temp file cleanup on exit
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer",
                "CleanupWizardRunOnBoot",
                1,
                RegistryValueKind2.DWord),
            // Enable disk cleanup suggestions
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer",
                "EnableAutoTray",
                0,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-windows-temp",
        NameEs = "Limpiar archivos temporales de Windows",
        NameEn = "Cleanup Windows temporary files",
        DescriptionEs = "Configura limpieza automática de archivos temporales de Windows mediante políticas de registro.",
        DescriptionEn = "Configures automatic cleanup of Windows temporary files via registry policies.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\Windows\\CurrentVersion. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Limpieza de archivos temporales configurada."));
    }
}
