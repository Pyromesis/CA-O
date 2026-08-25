using CAO.Infrastructure.Benchmarking;
using CAO.Shared;
using Xunit;

namespace CAO.Benchmark.Tests;

/// <summary>
/// Statistical benchmark semantics (FASE 21): median-of-trials absorbs
/// single-run noise; the noise floor blocks "improvement" claims under the
/// minimum effect threshold.
/// </summary>
public sealed class BenchmarkTrialsTests
{
    private static BenchmarkRunHeader Header(string workload) => new(
        workload, DateTime.UtcNow, 26200, GpuDriverVersion: "", Resolution: "", RefreshHz: 0, PowerState: "ac");

    [Fact]
    public void MedianOfTrialsAbsorbsASingleNoisyRun()
    {
        var results = new[]
        {
            new SystemBenchmarkResult(Header("t1"), CpuScore: 24_000, MemoryBandwidthGbs: 20.0, DiskReadMbs: 900, DiskWriteMbs: 800, Elapsed: TimeSpan.Zero),
            new SystemBenchmarkResult(Header("t2"), CpuScore: 24_100, MemoryBandwidthGbs: 20.1, DiskReadMbs: 905, DiskWriteMbs: 805, Elapsed: TimeSpan.Zero),
            // Outlier trial (background process spike) — must not move the medians.
            new SystemBenchmarkResult(Header("t3"), CpuScore: 60_000, MemoryBandwidthGbs: 45.0, DiskReadMbs: 2_000, DiskWriteMbs: 1_500, Elapsed: TimeSpan.Zero),
        };

        var median = SystemBenchmarkRunner.MedianOf(results);

        Assert.InRange(median.CpuScore, 24_000, 24_150);
        Assert.InRange(median.MemoryBandwidthGbs, 19.9, 20.2);
    }

    [Fact]
    public void EffectBelowNoiseFloorIsNeverAnImprovement()
    {
        var baseline = new SystemBenchmarkResult(Header("b"), CpuScore: 24_000, MemoryBandwidthGbs: 20, DiskReadMbs: 0, DiskWriteMbs: 0, Elapsed: TimeSpan.Zero);
        var after = baseline with { CpuScore = baseline.CpuScore * 1.004 }; // +0.4 %

        var comparison = SystemBenchmarkRunner.Compare(baseline, after);

        Assert.Equal("Sin mejora medible", comparison.VerdictEs);
        Assert.True(Math.Abs(comparison.CpuDeltaPercent) < SystemBenchmarkRunner.NoiseFloorPercent);
    }

    [Fact]
    public void RegressionBeyondNoiseFloorIsDetected()
    {
        var baseline = new SystemBenchmarkResult(Header("b"), CpuScore: 24_000, MemoryBandwidthGbs: 20, DiskReadMbs: 0, DiskWriteMbs: 0, Elapsed: TimeSpan.Zero);
        var after = baseline with { MemoryBandwidthGbs = baseline.MemoryBandwidthGbs * 0.90 }; // -10 %

        var comparison = SystemBenchmarkRunner.Compare(baseline, after);

        Assert.Equal("Regresión", comparison.VerdictEs);
    }
}
