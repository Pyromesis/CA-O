using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>Disables interrupt moderation on physical NICs that expose the setting.</summary>
public sealed class ConfigureInterruptModerationForLowLatency : NicAdvancedPropertyOptimization
{
    protected override string[] PropertyNames { get; } = ["*InterruptModeration"];

    protected override string EnabledText => "0";
    protected override string DisabledText => "1";

    public override OptimizationDefinition Definition => new()
    {
        Id = "configure-interrupt-moderation-for-low-latency",
        NameEs = "Moderar interrupciones para baja latencia",
        NameEn = "Set interrupt moderation for low latency",
        DescriptionEs = "Desactiva la moderación de interrupciones en NICs físicas que exponen el ajuste. Aumenta uso de CPU.",
        DescriptionEn = "Disables interrupt moderation on physical NICs exposing the setting. Increases CPU usage.",
        TooltipEs = "Modifica *InterruptModeration=0 solo donde ya existe. Reversible exacto. Solo perfil Competitive.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };
}
