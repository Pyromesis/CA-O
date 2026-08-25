namespace CAO.Shared;

/// <summary>
/// Analyze-first buckets shown instead of a blind "optimize everything"
/// action (spec 13). Every optimization lands in exactly one bucket after
/// the system analysis pipeline runs.
/// </summary>
public enum RecommendationBucket
{
    Recommended,
    Optional,
    Experimental,
    SecuritySensitive,
    NotApplicable,
}

/// <summary>Why the recommendation engine placed an optimization in a bucket.</summary>
public sealed record RecommendationReason(string Code, string MessageEs);

/// <summary>
/// Full user-facing recommendation card data (spec 79): what it does, current
/// vs recommended state, evidence, risk and rollback availability.
/// </summary>
public sealed record Recommendation
{
    public required string OptimizationId { get; init; }

    public required string NameEs { get; init; }

    public required string DescriptionEs { get; init; }

    public required OptimizationCategory Category { get; init; }

    public required OptimizationState CurrentState { get; init; }

    /// <summary>State the engine would set when applied.</summary>
    public required OptimizationState TargetState { get; init; }

    public required RecommendationBucket Bucket { get; init; }

    public required PerformanceImpact ExpectedImpact { get; init; }

    public required EvidenceLevel Evidence { get; init; }

    public required RiskLevel Risk { get; init; }

    public required SecurityImpact SecurityImpact { get; init; }

    public required CompatibilityStatus Compatibility { get; init; }

    public bool RequiresReboot => Flags.HasFlag(OptimizationFlags.RequiresReboot);

    public bool RollbackAvailable { get; init; }

    public bool AntiCheatConflictRisk { get; init; }

    /// <summary>0..100 composite score from OptimizationScoreCalculator.</summary>
    public int? Score { get; init; }

    public required RecommendationReason Reason { get; init; }

    public OptimizationFlags Flags { get; init; }
}
