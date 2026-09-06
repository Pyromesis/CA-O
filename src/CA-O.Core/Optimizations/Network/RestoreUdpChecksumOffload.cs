using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>Restores UDP checksum offloading on physical NICs that expose it.</summary>
public sealed class RestoreUdpChecksumOffload : NicAdvancedPropertyOptimization
{
    protected override string[] PropertyNames { get; } =
        ["*UDPChecksumOffloadIPv4", "*UDPChecksumOffloadIPv6"];

    protected override string EnabledText => "1";
    protected override string DisabledText => "0";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-udp-checksum-offload",
        NameEs = "Restaurar UDP checksum offload",
        NameEn = "Restore UDP checksum offload",
        DescriptionEs = "Activa el cálculo de checksum UDP en la NIC en adaptadores físicos que lo soportan.",
        DescriptionEn = "Enables UDP checksum offload on physical NICs that support it.",
        TooltipEs = "Modifica *UDPChecksumOffloadIPv4/IPv6 solo en adaptadores físicos que ya exponen la propiedad. Reversible exacto.",
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
