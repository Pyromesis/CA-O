using CAO.Infrastructure.Benchmarking;
using CAO.Shared;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>Task 5 (Plan 02): CSV de sesión, pendiente en UiState y claves nuevas.</summary>
public sealed class BenchmarkTask5Tests
{
    private static SystemBenchmarkResult Result(double cpu, double mem, double read, double write) =>
        new(new BenchmarkRunHeader("t", DateTime.UtcNow, 26200, "", "", 0, "ac"),
            CpuScore: cpu, MemoryBandwidthGbs: mem,
            DiskReadMbs: read, DiskWriteMbs: write, Elapsed: TimeSpan.Zero);

    [Fact]
    public void ExportSessionCsv_FormatsFourRowsWithDeltas()
    {
        var vm = new BenchmarkViewModel();
        var session = new BenchmarkSession(
            Result(50_000, 20, 400, 350),
            Result(55_000, 20, 460, 350),
            null, null, "Storage", "cleanup-windows-temp");

        var csv = vm.ExportSessionCsv(session);

        Assert.StartsWith("metrica;antes;despues;delta_%", csv);
        Assert.Contains("cpu_ops;50000.00;55000.00;+10.00", csv);
        Assert.Contains("mem_gbs;20.00;20.00;+0.00", csv);
        Assert.Contains("disk_r_mbs;400.00;460.00;+15.00", csv);
        Assert.Contains("disk_w_mbs;350.00;350.00;+0.00", csv);
    }

    [Fact]
    public void ExportSessionCsv_WithoutAfter_UsesZero()
    {
        var vm = new BenchmarkViewModel();
        var session = new BenchmarkSession(
            Result(50_000, 20, 400, 350), null, null, null, string.Empty, "manual");

        var csv = vm.ExportSessionCsv(session);

        Assert.Contains("cpu_ops;50000.00;0.00;-100.00", csv);
    }

    [Fact]
    public void UiState_PendingBenchmark_DefaultsEmpty_AndRoundTrips()
    {
        var state = new UiState();

        Assert.Equal(string.Empty, state.PendingBenchmarkOptimizationId);
        Assert.Equal(string.Empty, state.PendingBenchmarkCategory);

        state.PendingBenchmarkOptimizationId = "disable-vbs";
        state.PendingBenchmarkCategory = "Gaming";

        Assert.Equal("disable-vbs", state.PendingBenchmarkOptimizationId);
        Assert.Equal("Gaming", state.PendingBenchmarkCategory);
    }

    [Fact]
    public void Localizer_BenchmarkTask5Keys_ResolveInBothLanguages()
    {
        string[] keys =
        [
            "benchmark.measureOpt", "benchmark.measuring", "benchmark.dnsTitle",
            "benchmark.bootTitle", "benchmark.fluencyTitle", "benchmark.exportCsv",
            "benchmark.unavailable", "benchmark.ctxOpt",
        ];
        try
        {
            CAO.UI.Localizer.SetLanguage("es-ES");
            foreach (var key in keys) Assert.NotEqual(key, CAO.UI.Localizer.Get(key));
            CAO.UI.Localizer.SetLanguage("en-US");
            foreach (var key in keys) Assert.NotEqual(key, CAO.UI.Localizer.Get(key));
        }
        finally
        {
            CAO.UI.Localizer.SetLanguage("es-ES");
        }
    }
}
