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