using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>Restores Large Send Offload (LSO) on physical NICs that expose it.</summary>
public sealed class RestoreLargeSendOffload : NicAdvancedPropertyOptimization
{
    protected override string[] PropertyNames { get; } =
        ["*LsoV2IPv4", "*LsoV2IPv6"];

    protected override string EnabledText => "1";
    protected override string DisabledText => "0";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-large-send-offload",
        NameEs = "Restaurar Large Send Offload",
        NameEn = "Restore Large Send Offload",
        DescriptionEs = "Activa LSOv2 en adaptadores físicos que lo soportan. Mejora el envío de paquetes grandes.",
        DescriptionEn = "Enables LSOv2 on physical NICs that support it. Improves large packet sends.",
        TooltipEs = "Modifica *LsoV2IPv4/*LsoV2IPv6 solo en adaptadores físicos que ya exponen la propiedad. Reversible exacto.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };
}
