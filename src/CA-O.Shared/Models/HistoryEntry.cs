namespace CAO.Shared;

/// <summary>
/// One line of %ProgramData%\CA-O\history.jsonl (spec 74). Every field is
/// serializable JSON; sensitive data (tokens, credentials, personal input)
/// must never be stored here (spec 75).
/// </summary>
public sealed record HistoryEntry
{
    public required DateTime TimestampUtc { get; init; }

    public string? AppVersion { get; init; }

    public int WindowsBuild { get; init; }

    public required string OptimizationId { get; init; }

    /// <summary>"apply" | "revert" | "detect" | "verify" | "snapshot".</summary>
    public required string Operation { get; init; }

    public string User { get; init; } = string.Empty;

    /// <summary>Precondition phase outcome: "passed" | "failed" | "skipped".</summary>
    public string Precondition { get; init; } = "passed";

    public string? SnapshotId { get; init; }

    /// <summary>Apply phase outcome: "success" | "failed" | null when not applicable.</summary>
    public string? ApplyResult { get; init; }

    /// <summary>Verification outcome: "passed" | "failed" | "pending-reboot" | null.</summary>
    public string? Verification { get; init; }

    public bool Success { get; init; }

    /// <summary>True when a stored snapshot allows reverting this change.</summary>
    public bool RollbackAvailable { get; init; }

    /// <summary>Legacy compatibility fields kept for the tabular log reader.</summary>
    public string? PreviousState { get; init; }

    public string? NewState { get; init; }

    public string? Error { get; init; }
}

/// <summary>Minimal info about a Windows system restore point.</summary>
public sealed record RestorePointInfo
{
    public required DateTime CreationTime { get; init; }

    public required string Description { get; init; }

    public required int SequenceNumber { get; init; }
}
