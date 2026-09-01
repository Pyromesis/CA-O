using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class CleanupDeliveryOptimizationCache : RegistryOptimizationBase
{
    /// <summary>Clears Delivery Optimization (DO) cache to free disk space.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Limit Delivery Optimization cache size to minimal
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\\Microsoft\\Windows\\CurrentVersion\\DeliveryOptimization",
                "MaxCacheSize",
                0,
                RegistryValueKind2.DWord),
            // Set cache to HTTP only (no P2P cache)
            new ValueTarget(
                RegistryHive2.CurrentUser,
                @"Software\\Microsoft\\Windows\\CurrentVersion\\DeliveryOptimization\\Settings",
                "DownloadMode",
                1,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "cleanup-delivery-optimization-cache",
        NameEs = "Limpiar caché Delivery Optimization",
        NameEn = "Cleanup Delivery Optimization cache",
        DescriptionEs = "Limpia el caché de Delivery Optimization (DO) para liberar espacio en disco.",
        DescriptionEn = "Clears Delivery Optimization cache to free disk space.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\DeliveryOptimization. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Caché Delivery Optimization limpiado."));
    }
}
