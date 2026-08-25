namespace CAO.Shared;

/// <summary>Severity of a known issue.</summary>
public enum KnownIssueSeverity
{
    Information,
    Warning,
    Critical,
}

public enum KnownIssueStatus
{
    /// <summary>Real, confirmed issue shipped with the database.</summary>
    Active,

    /// <summary>Fixed in a later driver/build; kept for history matching.</summary>
    Resolved,

    /// <summary>Entry used to validate the matcher in tests/demos; never surfaced as Active.</summary>
    Sample,
}

/// <summary>
/// One versioned known-issue entry (spec 55): the database matches concrete
/// combinations of Windows build + driver + component + game and warns the
/// user before they waste time on unrelated tweaks.
/// </summary>
public sealed record KnownIssue
{
    public required string Id { get; init; }

    public required string SummaryEs { get; init; }

    public required string WorkaroundEs { get; init; }

    public required KnownIssueSeverity Severity { get; init; }

    public required KnownIssueStatus Status { get; init; }

    /// <summary>Inclusive Windows build range; null = any build.</summary>
    public int? MinWindowsBuild { get; init; }

    public int? MaxWindowsBuild { get; init; }

    /// <summary>Substring match against GPU/driver identifiers; null = any.</summary>
    public string? GpuVendorOrDriverContains { get; init; }

    /// <summary>Component kind hint: "usb" | "network" | "audio" | "gpu" | "storage"; null = any.</summary>
    public string? ComponentKind { get; init; }

    /// <summary>Game name substring; null = game-independent.</summary>
    public string? GameNameContains { get; init; }
}
