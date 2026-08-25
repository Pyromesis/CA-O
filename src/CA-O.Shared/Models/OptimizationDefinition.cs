namespace CAO.Shared;

/// <summary>
/// Static definition of one optimization: what it is, what it costs, how it
/// is gated. Behavior lives in <c>CA-O.Core.Optimizations</c>.
/// </summary>
public sealed record OptimizationDefinition
{
    public required string Id { get; init; }

    /// <summary>Spanish display name (primary language of the app).</summary>
    public required string NameEs { get; init; }

    public required string NameEn { get; init; }

    public required string DescriptionEs { get; init; }

    public required string DescriptionEn { get; init; }

    /// <summary>Detailed tooltip text shown on hover (warnings included).</summary>
    public string TooltipEs { get; init; } = string.Empty;

    public OptimizationCategory Category { get; init; }

    public ImpactLevel Impact { get; init; }

    /// <summary>Evidence-aware metadata used by recommendation and profile engines.</summary>
    public PerformanceImpact ExpectedImpact { get; init; } = PerformanceImpact.WorkloadDependent;

    public EvidenceLevel Evidence { get; init; } = EvidenceLevel.Unknown;

    public RiskLevel Risk { get; init; } = RiskLevel.Moderate;

    public CompatibilityStatus Compatibility { get; init; } = CompatibilityStatus.Unknown;

    public SecurityImpact SecurityImpact { get; init; } = SecurityImpact.Unknown;

    /// <summary>False only for operations that explicitly cannot restore prior state.</summary>
    public bool Reversible { get; init; } = true;

    public OptimizationFlags Flags { get; init; }
}
