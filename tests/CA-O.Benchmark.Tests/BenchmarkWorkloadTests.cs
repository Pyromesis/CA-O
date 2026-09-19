using CAO.Infrastructure.Benchmarking;
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
}
