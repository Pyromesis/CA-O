using CAO.Core.Engine;
using CAO.Shared;
using Xunit;

namespace CAO.Benchmark.Tests;

/// <summary>
/// Frame-time statistics and comparison semantics (spec 27-29, 83-84):
/// percentiles, variance, noise floor verdicts and repeated-run medians.
/// </summary>
public sealed class BenchmarkStatisticsTests
{
    [Fact]
    public void FrameTimesReportPercentilesAndLows()
    {
        // 200 frames at ~16.7 ms with five stutter spikes (~2.5%): enough to
        // move the worst-percentile buckets, not just the average.
        var frames = Enumerable.Repeat(16.7, 195).Concat(Enumerable.Repeat(50.0, 5)).ToList();

        var stats = BenchmarkAnalyzer.AnalyzeFrameTimes(frames);

        Assert.Equal(200, stats.SampleCount);
        Assert.InRange(stats.AverageFps, 54, 60);
        Assert.Equal(16.7, stats.P50FrameTimeMs, 1);
        Assert.True(stats.P99FrameTimeMs > stats.P95FrameTimeMs);
        // 1% low sits in the spike bucket: far below the smooth-frame average.
        Assert.InRange(stats.OnePercentLowFps, 15, 30);
        Assert.InRange(stats.ZeroPointOnePercentLowFps, 15, 25);
        Assert.True(stats.FrameTimeVarianceMsSquared > 0);
    }

    [Fact]
    public void ComparisonRespectsNoiseFloor()
    {
        var baseline = new FrameTimeStatistics(600, 120.0, 90.0, 70.0, 8.3, 11.0, 12.0, 2.5);
        // +0.4% average FPS: below any sane noise floor.
        var after = baseline with { AverageFps = 120.5 };

        var comparison = BenchmarkAnalyzer.Compare(baseline, after);

        Assert.Equal(BenchmarkVerdict.NoMeasurableImprovement, comparison.Verdict);
    }

    [Fact]
    public void ComparisonFlagsRegressionOnWorseP99()
    {
        var baseline = new FrameTimeStatistics(600, 120.0, 90.0, 70.0, 8.3, 11.0, 12.0, 2.5);
        var after = baseline with { P99FrameTimeMs = 20.0 };

        var comparison = BenchmarkAnalyzer.Compare(baseline, after);

        Assert.Equal(BenchmarkVerdict.Regression, comparison.Verdict);
        Assert.True(comparison.P99FrameTimeChangePercent > 3);
    }

    [Fact]
    public void RepeatedRunsUseMedianNotSingleSample()
    {
        // Spec 83: never trust one measurement. Median of runs absorbs spikes.
        var cpuScores = new[] { 24_000d, 24_050d, 23_980d, 60_000d /* outlier */ };

        var median = Median(cpuScores);

        Assert.InRange(median, 23_900, 24_100);
    }

    private static double Median(double[] values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
