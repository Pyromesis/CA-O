using System.Text.Json;
using CAO.Core.Overclock.Reporting;
using CAO.Infrastructure.Benchmarking;
using CAO.Infrastructure.Overclock.Reporting;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

/// <summary>
/// The projection is where a benchmark run becomes an overclock claim, so it
/// is where a wrong unit or a mis-graded evidence tier would first show up.
/// </summary>
public sealed class OverclockDeltaSourceTests
{
    private static readonly DateTime UtcInstant = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Project_MapsEveryBenchmarkAxisOntoTheDomainAxes()
    {
        var sample = BenchmarkDeltaSource.Project(Result(420.5, 51.25, 3200, 2100));

        Assert.Equal(420.5d, Value(sample, DeltaAxis.Cpu));
        Assert.Equal(51.25d, Value(sample, DeltaAxis.Memory));
        Assert.Equal(3200d, Value(sample, DeltaAxis.StorageRead));
        Assert.Equal(2100d, Value(sample, DeltaAxis.StorageWrite));
    }

    [Fact]
    public void Project_LabelsEachAxisWithTheUnitItWasMeasuredIn()
    {
        var sample = BenchmarkDeltaSource.Project(Result());

        Assert.Equal(BenchmarkDeltaSource.CpuUnitKey, sample.Axis(DeltaAxis.Cpu)!.Unit);
        Assert.Equal(BenchmarkDeltaSource.BandwidthUnitKey, sample.Axis(DeltaAxis.Memory)!.Unit);
        Assert.Equal(BenchmarkDeltaSource.ThroughputUnitKey, sample.Axis(DeltaAxis.StorageRead)!.Unit);
        Assert.Equal(BenchmarkDeltaSource.ThroughputUnitKey, sample.Axis(DeltaAxis.StorageWrite)!.Unit);
    }

    [Fact]
    public void Project_GradesEveryAxisAsBenchmark()
    {
        var sample = BenchmarkDeltaSource.Project(Result());

        // Reproducible measurement the user can re-run with the same button:
        // that is precisely what the Benchmark tier means, and grading it
        // higher would overstate what the number is worth.
        Assert.All(sample.Axes, axis => Assert.Equal(EvidenceLevel.Benchmark, axis.Evidence));
    }

    [Fact]
    public void Project_KeepsAZeroMeasurementAsZero_NotAsMissing()
    {
        var sample = BenchmarkDeltaSource.Project(Result(cpu: 0));

        // "Measured zero" and "not measured" have to stay distinguishable; the
        // builder is the one that refuses a percentage over a zero baseline.
        var cpu = sample.Axis(DeltaAxis.Cpu);
        Assert.NotNull(cpu);
        Assert.Equal(0d, cpu!.Value);
    }

    [Fact]
    public void Project_PreservesAUtcInstant()
    {
        var sample = BenchmarkDeltaSource.Project(Result(at: UtcInstant));

        Assert.Equal(new DateTimeOffset(UtcInstant), sample.TakenAtUtc);
    }

    [Fact]
    public void Project_ConvertsALocalInstantToUtc()
    {
        var local = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Local);

        var sample = BenchmarkDeltaSource.Project(Result(at: local));

        Assert.Equal(TimeSpan.Zero, sample.TakenAtUtc.Offset);
        Assert.Equal(local.ToUniversalTime(), sample.TakenAtUtc.UtcDateTime);
    }

    [Fact]
    public void Project_ReadsAnUnspecifiedInstantAsUtc_InsteadOfShiftingIt()
    {
        var sample = BenchmarkDeltaSource.Project(Result(at: new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Unspecified)));

        Assert.Equal(new DateTimeOffset(UtcInstant), sample.TakenAtUtc);
    }

    [Fact]
    public void Store_KeepsTheReportNextToTheBenchmarkArtifacts()
    {
        // Under the benchmarks directory on purpose: that is where
        // BenchmarkStore's atomic writer creates the folder, so reusing the
        // writer costs the path no meaning.
        Assert.StartsWith(CaOPaths.BenchmarksDirectory, FileOverclockDeltaStore.LastReportPath, StringComparison.Ordinal);
        Assert.EndsWith("overclock-delta.json", FileOverclockDeltaStore.LastReportPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_SurvivesAJsonRoundTrip()
    {
        var report = OverclockDeltaBuilder.Build(
            BenchmarkDeltaSource.Project(Result(100, 100, 100, 100, UtcInstant)),
            BenchmarkDeltaSource.Project(Result(120, 100, 100, 100, UtcInstant)));

        var restored = JsonSerializer.Deserialize<OverclockDeltaReport>(JsonSerializer.Serialize(report));

        Assert.NotNull(restored);
        Assert.Equal(report.Verdict, restored!.Verdict);
        Assert.Equal(report.NoiseFloorPercent, restored.NoiseFloorPercent);
        Assert.Equal(report.Axes.Count, restored.Axes.Count);
        Assert.Equal(
            report.Axes.Select(r => (r.Axis, r.Verdict, r.DeltaPercent)),
            restored.Axes.Select(r => (r.Axis, r.Verdict, r.DeltaPercent)));
        Assert.True(restored.HasData);
    }

    [Fact]
    public void Report_WithNoData_RoundTripsWithoutInventingOne()
    {
        var empty = OverclockDeltaReport.Empty;

        var restored = JsonSerializer.Deserialize<OverclockDeltaReport>(JsonSerializer.Serialize(empty));

        Assert.NotNull(restored);
        Assert.False(restored!.HasData);
        Assert.Equal(DeltaVerdict.NoData, restored.Verdict);
    }

    private static double Value(DeltaSample sample, DeltaAxis axis) => sample.Axis(axis)!.Value;

    private static SystemBenchmarkResult Result(
        double cpu = 400,
        double memory = 50,
        double read = 3000,
        double write = 2000,
        DateTime? at = null) =>
        new(
            new BenchmarkRunHeader(
                BenchmarkDeltaSource.WorkloadId,
                at ?? UtcInstant,
                26100,
                "580.68",
                "1920x1080",
                144,
                "High performance"),
            cpu,
            memory,
            read,
            write,
            TimeSpan.FromSeconds(12));
}