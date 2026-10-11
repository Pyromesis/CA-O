using System.Diagnostics;
using CAO.Core.Benchmark;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Core.Overclock.Reporting;
using CAO.Infrastructure.Benchmarking;
using CAO.Infrastructure.Logging;
using CAO.Infrastructure.Overclock;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

// Tripwires de regresion, no micro-benchmarks: cada test ejecuta un path de
// datos REAL de la app (I/O JSONL del historial, agregacion de trials,
// comparacion de deltas, validacion del catalogo) y falla solo si tarda un
// orden de magnitud mas de lo sano. Los limites son holgados a proposito
// (segundos, cuando lo sano son milisegundos) para no dar flakes en
// maquinas lentas; su trabajo es cazar cuelgues y patologias estilo 18 s,
// no medir medio milisegundo.
public sealed class OverclockPerformanceTests
{
    private static readonly TimeSpan HealthyLimit = TimeSpan.FromSeconds(15);

    private static void AssertHealthy(Stopwatch sw, string what)
    {
        sw.Stop();
        Assert.True(
            sw.Elapsed < HealthyLimit,
            $"{what} took {sw.Elapsed.TotalMilliseconds:0} ms (limit {HealthyLimit.TotalMilliseconds:0} ms)");
    }

    [Fact]
    public void History_Roundtrip_1000Entries_CompletesInSeconds()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cao-perf-{Guid.NewGuid():N}.jsonl");
        try
        {
            var log = new JsonHistoryLogger(path);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                log.Log(new HistoryEntry
                {
                    OptimizationId = $"perf-{i}",
                    Operation = "apply",
                    TimestampUtc = DateTime.UtcNow,
                    Success = true,
                });
            }

            IReadOnlyList<HistoryEntry> all = log.ReadLast(10000);
            AssertHealthy(sw, "History roundtrip (1000 appends + read-all)");

            Assert.Equal(1000, all.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Benchmark_MedianOf_NineTrials_CompletesInSeconds()
    {
        var trials = new List<SystemBenchmarkResult>();
        for (int i = 0; i < 9; i++)
        {
            trials.Add(new SystemBenchmarkResult(
                new BenchmarkRunHeader(
                    "perf", DateTime.UtcNow, 26100, "drv", "1920x1080", 144, "High performance"),
                100d + i,
                20d,
                500d,
                400d,
                TimeSpan.FromSeconds(1)));
        }

        var sw = Stopwatch.StartNew();
        SystemBenchmarkResult median = SystemBenchmarkRunner.MedianOf(trials);
        AssertHealthy(sw, "SystemBenchmarkRunner.MedianOf (9 trials)");

        Assert.Equal(104d, median.CpuScore);
    }

    [Fact]
    public void Delta_Build_CompletesInSeconds()
    {
        var baseline = new DeltaSample(
            DateTimeOffset.UtcNow,
            new[] { new AxisMeasurement(DeltaAxis.Cpu, 100d, "pts", EvidenceLevel.Benchmark) });
        var candidate = new DeltaSample(
            DateTimeOffset.UtcNow,
            new[] { new AxisMeasurement(DeltaAxis.Cpu, 110d, "pts", EvidenceLevel.Benchmark) });

        var sw = Stopwatch.StartNew();
        OverclockDeltaReport report = OverclockDeltaBuilder.Build(baseline, candidate);
        AssertHealthy(sw, "OverclockDeltaBuilder.Build");

        Assert.True(report.HasData);
    }

    [Fact]
    public void Catalog_Validate_AllKnobs_CompletesInSeconds()
    {
        var sw = Stopwatch.StartNew();
        var validation = CatalogValidator.Validate(OverclockReadOnlyRegistry.AllCatalogKnobs());
        AssertHealthy(sw, "CatalogValidator.Validate (full catalog)");

        Assert.True(validation.IsValid);
    }
}
