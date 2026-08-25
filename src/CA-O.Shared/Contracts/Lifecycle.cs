namespace CAO.Shared;

/// <summary>Result of precondition evaluation before any mutation is attempted.</summary>
public sealed record PreconditionResult(bool Passed, string ReasonEs)
{
    public static PreconditionResult Ok(string reasonEs) => new(true, reasonEs);

    public static PreconditionResult Fail(string reasonEs) => new(false, reasonEs);
}

/// <summary>Outcome of an Apply operation.</summary>
public sealed record ApplyResult(bool Success, string MessageEs, string? Error = null)
{
    public static ApplyResult Ok(string messageEs) => new(true, messageEs);

    public static ApplyResult Fail(string messageEs, string? error = null) => new(false, messageEs, error);
}

/// <summary>Outcome of a post-apply verification pass.</summary>
public sealed record VerificationResult(
    bool Verified,
    OptimizationState ObservedState,
    string MessageEs)
{
    public static VerificationResult Passed(OptimizationState state, string messageEs) =>
        new(true, state, messageEs);

    public static VerificationResult Failed(OptimizationState state, string messageEs) =>
        new(false, state, messageEs);
}

/// <summary>Outcome of a rollback attempt against a captured snapshot.</summary>
public sealed record RollbackResult(bool Success, string MessageEs, string? Error = null)
{
    public static RollbackResult Ok(string messageEs) => new(true, messageEs);

    public static RollbackResult Fail(string messageEs, string? error = null) => new(false, messageEs, error);
}

/// <summary>
/// Identifies a stored snapshot without embedding its content; the store maps
/// this id to the full captured state on disk.
/// </summary>
public sealed record SnapshotDescriptor(string SnapshotId, DateTime TimestampUtc, int EntryCount);

/// <summary>Phases of the transactional model (spec 123).</summary>
public enum TransactionPhase
{
    NotStarted,
    Precheck,
    Snapshot,
    Apply,
    Verify,
    Commit,
    RolledBack,
    Failed,
}
