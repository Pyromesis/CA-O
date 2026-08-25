using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.PrivacySecurity;

/// <summary>#5: SubscribedContent-338388Enabled=0 (suggested apps/notifications).</summary>
public sealed class DisableSuggestions : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar notificaciones y sugerencias",
        NameEn = "Disable notifications and suggestions",
        DescriptionEs = "Apaga las sugerencias de apps y contenido promocional de Windows.",
        DescriptionEn = "Turns off Windows suggested apps and promotional content.",
        TooltipEs = "SubscribedContent-338388Enabled=0 (sugerencias de aplicaciones en Configuración/Menú Inicio).",
        Category = OptimizationCategory.PrivacySecurity,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.PrivacyOnly,
        Impact = ImpactLevel.Low,
    };

    private static string Id => "disable-suggestions";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Sugerencias desactivadas."));
    }
}
