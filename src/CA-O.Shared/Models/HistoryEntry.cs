namespace CAO.Shared;

/// <summary>One line of the change history log.</summary>
public sealed record HistoryEntry
{
    public required DateTime TimestampUtc { get; init; }

    public required string User { get; init; }

    public required string OptimizationId { get; init; }

    /// <summary>"apply" | "revert" | "detect".</summary>
    public required string Action { get; init; }

    public required bool Success { get; init; }

    /// <summary>Previous raw value(s) captured by the snapshot, if any.</summary>
    public string? PreviousState { get; init; }

    /// <summary>New raw value(s) after the operation, if any.</summary>
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
