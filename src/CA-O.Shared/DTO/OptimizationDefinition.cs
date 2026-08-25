namespace CAO.Shared;

/// <summary>
/// Static definition of one optimization (spec 8): what it is, what it
/// costs, how it is gated and how it interacts with security/anti-cheat.
/// Behavior lives in implementations; this record is pure metadata that
/// powers recommendation, scoring, profiles and UI cards.
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

    /// <summary>Free-form tags for filtering/search (spec 8 "Tags").</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public ImpactLevel Impact { get; init; }

    /// <summary>Evidence-aware metadata used by recommendation and profile engines.</summary>
    public PerformanceImpact ExpectedImpact { get; init; } = PerformanceImpact.WorkloadDependent;

    public EvidenceLevel Evidence { get; init; } = EvidenceLevel.Unknown;

    /// <summary>How much the evidence can be trusted (spec 9). Contract tests reject Unknown in Recommended.</summary>
    public Confidence Confidence { get; init; } = Confidence.Unknown;

    public RiskLevel Risk { get; init; } = RiskLevel.Moderate;

    public CompatibilityStatus Compatibility { get; init; } = CompatibilityStatus.Unknown;

    public SecurityImpact SecurityImpact { get; init; } = SecurityImpact.Unknown;

    /// <summary>Relation to anti-cheat software (spec 49).</summary>
    public AntiCheatImpact AntiCheatImpact { get; init; } = AntiCheatImpact.Unknown;

    /// <summary>False only for operations that explicitly cannot restore prior state.</summary>
    public bool Reversible { get; init; } = true;

    public OptimizationFlags Flags { get; init; }

    /// <summary>Alias consumed by cards/report tables (spec 8 "RequiresRestart").</summary>
    public bool RequiresRestart => Flags.HasFlag(OptimizationFlags.RequiresReboot);

    /// <summary>True when apply only succeeds elevated (service-mediated).</summary>
    public bool RequiresAdmin => !Flags.HasFlag(OptimizationFlags.NotReversible) || true;

    /// <summary>Ids of optimizations that must be evaluated/handled first.</summary>
    public IReadOnlyList<string> Prerequisites { get; init; } = Array.Empty<string>();

    /// <summary>Ids of optimizations that cannot coexist with this one.</summary>
    public IReadOnlyList<string> Conflicts { get; init; } = Array.Empty<string>();

    /// <summary>Per-optimization documentation anchor inside docs/OPTIMIZATION-CATALOG.md.</summary>
    public string DocumentationId => Id;
}
