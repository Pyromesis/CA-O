using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Performance;

/// <summary>#7: BackgroundAccessApplications\GlobalUserDisabled=1.</summary>
public sealed class DisableBackgroundApps : RegistryOptimizationBase
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";

    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, KeyPath, "GlobalUserDisabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar apps en segundo plano",
        NameEn = "Disable background apps",
        DescriptionEs = "Impide que las aplicaciones de la tienda se ejecuten en segundo plano.",
        DescriptionEn = "Prevents store apps from running in the background.",
        TooltipEs = "GlobalUserDisabled=1 para el usuario actual. Las notificaciones en vivo de apps UWP pueden dejar de actualizarse.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.NoKnownConflict,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.NoKnownConflict,
        SecurityImpact = SecurityImpact.PrivacyOnly,
        Impact = ImpactLevel.Medium,
    };

    private static string Id => "disable-background-apps";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Apps en segundo plano desactivadas."));
    }
}
