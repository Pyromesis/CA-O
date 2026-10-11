using CAO.Core.Overclock.Reporting;
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock.Reporting;

/// <summary>
/// Produces the samples the delta builder compares. Behind an interface so
/// the ViewModel can be exercised against a canned sample, with no hardware
/// and without paying for a real benchmark run.
/// </summary>
public interface IOverclockDeltaSource
{
    ValueTask<DeltaSample> CaptureAsync(CancellationToken ct);
}

/// <summary>
/// Measures with the shared system benchmark. Trials, warmup and the noise
/// floor all come from <c>BenchmarkPolicy</c>, so an overclock claim is graded
/// by exactly the same ruler as every other number CA-O reports.
/// </summary>
public sealed class BenchmarkDeltaSource : IOverclockDeltaSource
{
    /// <summary>Stamped on the run header so a saved delta is traceable to its workload.</summary>
    public const string WorkloadId = "overclock-delta";

    public const string CpuUnitKey = "overclock-unit-score";
    public const string BandwidthUnitKey = "overclock-unit-gbs";
    public const string ThroughputUnitKey = "overclock-unit-mbs";

    private readonly SystemBenchmarkRunner _runner;

    public BenchmarkDeltaSource()
        : this(new SystemBenchmarkRunner())
    {
    }

    public BenchmarkDeltaSource(SystemBenchmarkRunner runner) => _runner = runner;

    public async ValueTask<DeltaSample> CaptureAsync(CancellationToken ct)
    {
        // Trials and warmup are left at the runner's defaults, which already
        // are BenchmarkPolicy.MeasuredRuns and WarmupRuns. Repeating the numbers
        // here would only give them a second place to drift out of step with
        // the noise floor they are graded against.
        var result = await _runner
            .RunTrialsAsync(workloadId: WorkloadId, ct: ct)
            .ConfigureAwait(false);

        return Project(result);
    }

    /// <summary>
    /// Projects a benchmark run onto the domain axes. This mapping lives in
    /// Infrastructure because Core cannot reference it, and it is not worth
    /// reversing a dependency for a projection. Every axis is graded
    /// <see cref="EvidenceLevel.Benchmark"/>: it is a measurement the user can
    /// reproduce with the same button, which is exactly what that tier means.
    /// </summary>
    public static DeltaSample Project(SystemBenchmarkResult result)
    {
        var takenAt = ToUtc(result.Header.TimestampUtc);
        return new DeltaSample(
            takenAt,
            [
                new AxisMeasurement(DeltaAxis.Cpu, result.CpuScore, CpuUnitKey, EvidenceLevel.Benchmark),
                new AxisMeasurement(DeltaAxis.Memory, result.MemoryBandwidthGbs, BandwidthUnitKey, EvidenceLevel.Benchmark),
                new AxisMeasurement(DeltaAxis.StorageRead, result.DiskReadMbs, ThroughputUnitKey, EvidenceLevel.Benchmark),
                new AxisMeasurement(DeltaAxis.StorageWrite, result.DiskWriteMbs, ThroughputUnitKey, EvidenceLevel.Benchmark),
            ]);
    }

    private static DateTimeOffset ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => new DateTimeOffset(value),
        DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),

        // Unspecified is read as UTC instead of as local: shifting a header
        // written by another process would silently re-date the report, and a
        // wrong instant is worse than an imprecise one.
        _ => new DateTimeOffset(value, TimeSpan.Zero),
    };
}