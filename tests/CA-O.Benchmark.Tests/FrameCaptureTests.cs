using CAO.Core.Engine;
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;
using Xunit;

namespace CAO.Benchmark.Tests;

public sealed class FrameCaptureTests
{
    private sealed class FakeCapture : IFrameCapture
    {
        public Task<FrameCaptureResult?> CaptureAsync(TimeSpan duration, CancellationToken ct)
        {
            // DESVIACIÓN del brief (documentada en task-4-report.md): el brief
            // pedía 595×16.7 + 5×50 (0.83% spikes), pero con 600 muestras el P99
            // cae en el índice 593 < 595, es decir, P99 = 16.7 y el 1% low
            // (59.9 fps) quedaría POR ENCIMA del promedio (58.9 fps), rompiendo
            // el invariante OnePercentLowFps < AverageFps. Se usan 10 spikes
            // (1.67%, misma convención que BenchmarkStatisticsTests: los spikes
            // deben superar el 1% para mover el bucket P99).
            var frames = Enumerable.Repeat(16.7, 590).Concat(Enumerable.Repeat(50.0, 10)).ToList();
            return Task.FromResult<FrameCaptureResult?>(new(frames, DpcPercent: 0.4, InterruptPercent: 0.2));
        }
    }

    [Fact]
    public async Task CaptureResult_FeedsAnalyzer_AvgAndLows()
    {
        IFrameCapture capture = new FakeCapture();
        var result = await capture.CaptureAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.NotNull(result);
        var stats = BenchmarkAnalyzer.AnalyzeFrameTimes(result!.FrameTimesMs);

        Assert.Equal(600, stats.SampleCount);
        Assert.InRange(stats.AverageFps, 54, 60);
        Assert.True(stats.OnePercentLowFps < stats.AverageFps);
        Assert.True(stats.P99FrameTimeMs > stats.P95FrameTimeMs);
    }

    [Fact]
    public void BootInfo_ReturnsSaneUptime_OrNull()
    {
        var boot = BootInfoProvider.GetBootInfo();
        // En CI sin WMI puede ser null: el test solo exige no-crash y cordura si hay datos.
        if (boot is not null)
            Assert.True(boot.BootTimeUtc <= DateTime.UtcNow);
    }
}
