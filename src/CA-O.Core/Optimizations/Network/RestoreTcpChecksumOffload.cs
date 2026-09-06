using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>Restores TCP checksum offloading on physical NICs that expose it.</summary>
public sealed class RestoreTcpChecksumOffload : NicAdvancedPropertyOptimization
{
    protected override string[] PropertyNames { get; } =
        ["*TCPChecksumOffloadIPv4", "*TCPChecksumOffloadIPv6"];

    protected override string EnabledText => "1";
    protected override string DisabledText => "0";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-tcp-checksum-offload",
        NameEs = "Restaurar TCP checksum offload",
        NameEn = "Restore TCP checksum offload",
        DescriptionEs = "Activa el cálculo de checksum TCP en la NIC en adaptadores físicos que lo soportan. Descarga a la CPU.",
        DescriptionEn = "Enables TCP checksum offload on physical NICs that support it. Offloads the CPU.",
        TooltipEs = "Modifica *TCPChecksumOffloadIPv4/IPv6 solo en adaptadores físicos que ya exponen la propiedad. Reversible exacto.",
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
