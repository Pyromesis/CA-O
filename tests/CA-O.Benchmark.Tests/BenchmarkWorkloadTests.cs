using CAO.Infrastructure.Benchmarking;
using CAO.Shared;
using Xunit;

namespace CAO.Benchmark.Tests;

public sealed class BenchmarkWorkloadTests
{
    [Fact]
    public async Task RunAsync_ReportsPositiveCpuOpsPerSecond()
    {
        var runner = new SystemBenchmarkRunner();
        // Solo CPU+MEM rápidas aquí; el disco se cubre en Task 2 ( Medir disco I/O en CI es lento ).
        var result = await runner.RunAsync("cpu-only", CancellationToken.None);

        Assert.True(result.CpuScore > 0, "CpuScore debe ser ops/s > 0, no un conteo constante.");
        Assert.True(result.CpuMs > 0, "CpuMs debe medir tiempo real de la carga.");
        Assert.True(result.MemoryBandwidthGbs > 0, "El ancho de banda de memoria debe ser > 0.");
    }

    [Fact]
    public async Task RunTrialsAsync_RespectsWarmupParameter_AndReportsCv()
    {
        var runner = new SystemBenchmarkRunner();
        var result = await runner.RunTrialsAsync(trials: 2, warmup: false, workloadId: "no-warmup", ct: CancellationToken.None);

        Assert.Equal("no-warmup", result.WorkloadId);
        Assert.True(result.CpuCvPercent >= 0, "Debe reportar CV% aunque sea 0 con 2 trials.");
        Assert.True(result.MemoryCvPercent >= 0);
    }

    [Fact]
    public void StorageCategory_WinsOnDisk_EvenWhenCpuFlat()
    {
        var header = new BenchmarkRunHeader("b", DateTime.UtcNow, 26200, "", "", 0, "ac");
        var baseline = new SystemBenchmarkResult(header, CpuScore: 50_000, MemoryBandwidthGbs: 20, DiskReadMbs: 400, DiskWriteMbs: 350, Elapsed: TimeSpan.Zero);
        var after = baseline with { DiskReadMbs = 460, DiskWriteMbs = 350 }; // +15 % lectura

        var comparison = SystemBenchmarkRunner.CompareFull(baseline, after, OptimizationCategory.Storage);

        Assert.Equal("Mejora medible", comparison.VerdictEs);
        Assert.Contains("disco", comparison.ReasonEs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NullCategory_KeepsLegacyCpuMemoryVerdict()
    {
        var header = new BenchmarkRunHeader("b", DateTime.UtcNow, 26200, "", "", 0, "ac");
        var baseline = new SystemBenchmarkResult(header, CpuScore: 50_000, MemoryBandwidthGbs: 20, DiskReadMbs: 400, DiskWriteMbs: 350, Elapsed: TimeSpan.Zero);
        var after = baseline with { DiskReadMbs = 800 }; // +100 % disco, CPU/MEM planas

        var comparison = SystemBenchmarkRunner.CompareFull(baseline, after);

        Assert.Equal("Sin mejora medible", comparison.VerdictEs); // compat: sin categoría, CPU/MEM mandan
    }
}
