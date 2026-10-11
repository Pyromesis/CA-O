namespace CAO.Core.Overclock;

/// <summary>A single knob read from the hardware.</summary>
public sealed record KnobRead(bool Success, KnobValue Value, string? FailureReasonKey)
{
    public static KnobRead Ok(KnobValue value) => new(true, value, null);

    public static KnobRead Fail(string reasonKey) => new(false, default, reasonKey);
}

/// <summary>Outcome of writing one knob value.</summary>
public sealed record KnobWriteResult(
    bool Success,
    KnobValue? Applied,
    string? FailureReasonKey,
    bool DriverBlocked)
{
    public static KnobWriteResult Ok(KnobValue applied) => new(true, applied, null, false);

    public static KnobWriteResult Fail(string reasonKey) => new(false, null, reasonKey, false);

    /// <summary>
    /// The installed driver refused the setter. The section degrades to
    /// read-only and reports the driver version instead of retrying blindly
    /// (risk R1).
    /// </summary>
    public static KnobWriteResult Blocked(string reasonKey) => new(false, null, reasonKey, true);
}

/// <summary>
/// Full pre-change state of one component, enough to restore it exactly.
/// Written to disk before any mutation happens.
/// </summary>
public sealed record UnitSnapshot(
    OverclockComponent Component,
    IReadOnlyList<KeyValuePair<KnobId, KnobValue>> Values,
    DateTimeOffset CapturedAt,
    string? DriverVersion)
{
    public int Count => Values.Count;
}

/// <summary>Outcome of restoring a <see cref="UnitSnapshot"/>.</summary>
public sealed record RestoreOutcome(
    bool Success,
    int RestoredCount,
    bool Verified,
    string? FailureReasonKey)
{
    public static RestoreOutcome Ok(int restored, bool verified) => new(true, restored, verified, null);

    public static RestoreOutcome Fail(string reasonKey) => new(false, 0, false, reasonKey);
}

/// <summary>An operation the catalog cannot express as a knob read/write.</summary>
public sealed record OverclockOperation(string Id, OverclockComponent Component, IReadOnlyDictionary<string, string> Arguments);

/// <summary>Outcome of an extension operation.</summary>
public sealed record ExtensionResult(bool Success, string? Detail, string? FailureReasonKey)
{
    public static ExtensionResult Ok(string? detail = null) => new(true, detail, null);

    public static ExtensionResult Fail(string reasonKey) => new(false, null, reasonKey);
}