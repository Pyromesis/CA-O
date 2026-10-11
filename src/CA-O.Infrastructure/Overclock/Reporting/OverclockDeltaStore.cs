using CAO.Core.Overclock.Reporting;
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock.Reporting;

/// <summary>
/// Where the last honest report is kept. Behind an interface so the page can
/// be tested against an in-memory store instead of writing to ProgramData.
/// </summary>
public interface IOverclockDeltaStore
{
    Task SaveAsync(OverclockDeltaReport report, CancellationToken ct);

    /// <summary>Returns null when nothing was saved, or the file is unreadable.</summary>
    Task<OverclockDeltaReport?> LoadAsync(CancellationToken ct);
}

/// <summary>
/// Persists next to the benchmark artifacts, reusing <see cref="BenchmarkStore"/>'s
/// atomic write (tmp + move) instead of growing a second writer with the same
/// guarantee: two writers is how half-written JSON starts appearing, and
/// half-written JSON reads back as "no data" on a run that did have data.
/// </summary>
public sealed class FileOverclockDeltaStore : IOverclockDeltaStore
{
    public static string LastReportPath =>
        Path.Combine(CaOPaths.BenchmarksDirectory, "overclock-delta.json");

    public Task SaveAsync(OverclockDeltaReport report, CancellationToken ct) =>
        BenchmarkStore.SaveJsonAsync(LastReportPath, report, ct);

    public Task<OverclockDeltaReport?> LoadAsync(CancellationToken ct) =>
        BenchmarkStore.LoadJsonAsync<OverclockDeltaReport>(LastReportPath, ct);
}