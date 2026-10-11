namespace CAO.Core.Overclock.Reporting;

/// <summary>
/// Where the real limits of a machine come from. Each backend that can write
/// answers with the range the driver itself reports, so "this setting could not
/// go past this value" is a fact read from the hardware rather than a number
/// CA-O decided to stop at.
///
/// Kept in Core because the ceiling is a domain fact, not a driver detail: the
/// interface mentions no vendor type, and adding one later means adding an
/// implementation, never changing this contract.
/// </summary>
public interface IOverclockCeilingSource
{
    /// <summary>
    /// The bounds the driver reports for every setting this source can write.
    /// Implementations return one entry per writable knob even when they cannot
    /// read the bound: a row with no numbers and a real reason is more useful
    /// than a missing row, which reads as "nothing bounded it".
    /// </summary>
    ValueTask<IReadOnlyList<TuningCeiling>> ReadAsync(CancellationToken ct);
}