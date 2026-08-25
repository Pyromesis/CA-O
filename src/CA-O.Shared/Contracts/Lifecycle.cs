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

/// <summary>Strict verification outcomes (FASE 10): Unknown is NEVER success.</summary>
public enum VerificationStatus
{
    /// <summary>Live state semantically equals the expected post-state.</summary>
    Passed,

    /// <summary>Live state contradicts the expected post-state.</summary>
    Failed,

    /// <summary>State could not be determined. Reported as-is, never as pass.</summary>
    Unknown,

    /// <summary>Maintenance actions without observable post-state.</summary>
    NotApplicable,
}

/// <summary>Outcome of a post-apply verification pass.</summary>
public sealed record VerificationResult(
    VerificationStatus Status,
    OptimizationState ObservedState,
    string MessageEs)
{
    public bool Verified => Status is VerificationStatus.Passed;

    public static VerificationResult Passed(OptimizationState state, string messageEs) =>
        new(VerificationStatus.Passed, state, messageEs);

    public static VerificationResult PendingReboot(string messageEs) =>
        new(VerificationStatus.Passed, OptimizationState.PendingReboot, messageEs);

    public static VerificationResult Unknown(OptimizationState state, string messageEs) =>
        new(VerificationStatus.Unknown, state, messageEs);

    public static VerificationResult Failed(OptimizationState state, string messageEs) =>
        new(VerificationStatus.Failed, state, messageEs);

    public static VerificationResult NotApplicable() =>
        new(VerificationStatus.NotApplicable, OptimizationState.Unknown,
            "Acción de mantenimiento sin estado posterior verificable.");
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
