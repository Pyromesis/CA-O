using CAO.Infrastructure.Benchmarking;
using CAO.Shared;
using Xunit;

namespace CAO.Benchmark.Tests;

public sealed class BenchmarkStoreTests
{
    private static SystemBenchmarkResult Result(string workload, string power = "ac", string hash = "abc123") =>
        new(new BenchmarkRunHeader(workload, DateTime.UtcNow, 26200, "", "", 0, power, MachineHash: hash),
            CpuScore: 50_000, MemoryBandwidthGbs: 20, DiskReadMbs: 500, DiskWriteMbs: 450, Elapsed: TimeSpan.FromSeconds(9));

    [Fact]
    public void MachineHash_IsStable_AndShort_AndHidesInput()
    {
        var a = BenchmarkStore.ComputeMachineHash("guid-1", "cpu-x");
        var b = BenchmarkStore.ComputeMachineHash("guid-1", "cpu-x");
        var c = BenchmarkStore.ComputeMachineHash("guid-2", "cpu-x");

        Assert.Equal(a, b);
        Assert.Equal(16, a.Length);
        Assert.NotEqual(a, c);
        Assert.DoesNotContain("guid-1", a);
    }

    [Fact]
    public void Baseline_ExpiresAfterTtl_OrOnPowerOrHashChange()
    {
        var baseline = Result("baseline");
        Assert.True(BenchmarkStore.IsBaselineValid(baseline, Result("after"), DateTime.UtcNow));
        Assert.False(BenchmarkStore.IsBaselineValid(baseline, Result("after", power: "battery"), DateTime.UtcNow));
        Assert.False(BenchmarkStore.IsBaselineValid(baseline, Result("after", hash: "otro"), DateTime.UtcNow));
        Assert.False(BenchmarkStore.IsBaselineValid(baseline, Result("after"), DateTime.UtcNow.AddDays(8)));
    }

    [Fact]
    public void Session_RoundTrips_WithCategory()
    {
        var session = new BenchmarkSession(Result("b"), null, null, null, "Storage", "cleanup-windows-temp");
        var json = System.Text.Json.JsonSerializer.Serialize(session);
        var back = System.Text.Json.JsonSerializer.Deserialize<BenchmarkSession>(json);

        Assert.NotNull(back);
        Assert.Equal("Storage", back!.Category);
        Assert.Equal("cleanup-windows-temp", back.OptimizationId);
    }
}
