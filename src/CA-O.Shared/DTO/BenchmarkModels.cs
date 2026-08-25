namespace CAO.Shared;

public enum BenchmarkVerdict
{
    InsufficientData,
    NoMeasurableImprovement,
    Improvement,
    Regression,
}

public sealed record FrameTimeStatistics(
    int SampleCount,
    double AverageFps,
    double OnePercentLowFps,
    double ZeroPointOnePercentLowFps,
    double P50FrameTimeMs,
    double P95FrameTimeMs,
    double P99FrameTimeMs,
    double FrameTimeVarianceMsSquared);

public sealed record BenchmarkComparison(
    BenchmarkVerdict Verdict,
    double AverageFpsChangePercent,
    double OnePercentLowChangePercent,
    double P99FrameTimeChangePercent,
    string ReasonEs);

/// <summary>
/// A single reproducible benchmark run (spec 69): workload identity plus the
/// environment facts needed to compare runs fairly.
/// </summary>
public sealed record BenchmarkRunHeader(
    string WorkloadId,
    DateTime TimestampUtc,
    int WindowsBuild,
    string GpuDriverVersion,
    string Resolution,
    int RefreshHz,
    string PowerState);

/// <summary>
/// Measured outcome attached to an optimization lifecycle (spec 66-70).
/// Null means "this optimization has no benchmark path"; never invent numbers.
/// </summary>
public sealed record BenchmarkResult
{
    public required BenchmarkRunHeader Header { get; init; }

    public FrameTimeStatistics? FrameTimes { get; init; }

    /// <summary>Generic scalar metric (e.g. MB/s disk throughput) with its unit.</summary>
    public double? Metric { get; init; }

    public string MetricName { get; init; } = string.Empty;

    public string MetricUnit { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, double> ExtraMetrics { get; init; } =
        new Dictionary<string, double>();
}
