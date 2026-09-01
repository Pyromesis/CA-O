using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

public sealed class EnableRss : RegistryOptimizationBase
{
    /// <summary>Enables Receive Side Scaling (RSS) for network performance on multi-core systems.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Enable RSS for supported NICs
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
                "EnableRss",
                1,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-rss",
        NameEs = "Habilitar Receive Side Scaling",
        NameEn = "Enable Receive Side Scaling",
        DescriptionEs = "Habilita RSS en NIC compatibles para distribuir procesamiento de red entre núcleos.",
        DescriptionEn = "Enables RSS on supported NICs to distribute network processing across cores.",
        TooltipEs = "Modifica HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip. Reversible via snapshot.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Receive Side Scaling habilitado."));
    }
}