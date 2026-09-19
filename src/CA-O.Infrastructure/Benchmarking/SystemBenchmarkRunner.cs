using System.Diagnostics;
using CAO.Shared;

namespace CAO.Infrastructure.Benchmarking;

/// <summary>Measured result of one system micro-benchmark run.</summary>
public sealed record SystemBenchmarkResult(
    BenchmarkRunHeader Header,
    double CpuScore,
    double MemoryBandwidthGbs,
    double DiskReadMbs,
    double DiskWriteMbs,
    TimeSpan Elapsed,
    double CpuMs = 0,
    double CpuCvPercent = 0,
    double MemoryCvPercent = 0,
    double DiskReadIops = 0,
    double DiskWriteIops = 0,
    Dictionary<string, double>? PhaseSeconds = null)
{
    public string WorkloadId => Header.WorkloadId;
}

/// <summary>Verdict of an A/B comparison with a noise floor (spec 108).</summary>
public sealed record SystemBenchmarkComparison(double CpuDeltaPercent, double MemoryDeltaPercent, string VerdictEs);

/// <summary>Comparación completa incluyendo disco (informativo, no decide veredicto).</summary>
public sealed record SystemBenchmarkFullComparison(
    double CpuDeltaPercent,
    double MemoryDeltaPercent,
    double DiskReadDeltaPercent,
    double DiskWriteDeltaPercent,
    string VerdictEs);

/// <summary>
/// Reproducible system benchmark (spec 66, 69): fixed workload sizes, header
/// records environment facts so runs are comparable. No invented numbers:
/// everything is measured in-process without spawning external tools.
/// </summary>
public sealed class SystemBenchmarkRunner
{
    /// <summary>Below this delta the change is declared "no measurable improvement" (single source: BenchmarkPolicy).</summary>
    public const double NoiseFloorPercent = Core.Benchmark.BenchmarkPolicy.MinimumEffectPercent;

    /// <summary>
    /// Scientific run (FASE 21): optional warmup pass (discarded), then N
    /// trials; every metric reports the MEDIAN, so a single noisy trial
    /// cannot manufacture an improvement.
    /// </summary>
    public async Task<SystemBenchmarkResult> RunTrialsAsync(
        int trials = 3,
        bool warmup = true,
        string workloadId = "system-baseline",
        CancellationToken ct = default)
    {
        var warmupRuns = warmup ? Core.Benchmark.BenchmarkPolicy.WarmupRuns : 0;
        for (var warm = 0; warm < warmupRuns; warm++)
        {
            await RunAsync(workloadId + "-warmup" + (warm + 1), ct);
        }

        var results = new List<SystemBenchmarkResult>(trials);
        for (var trial = 0; trial < trials; trial++)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await RunAsync($"{workloadId}-t{trial + 1}", ct));
        }

        var median = MedianOf(results);
        static double Cv(IEnumerable<double> values)
        {
            var arr = values.ToArray();
            if (arr.Length < 2) return 0;
            var avg = arr.Average();
            if (avg == 0) return 0;
            var sd = Math.Sqrt(arr.Average(v => (v - avg) * (v - avg)));
            return Math.Round(sd / avg * 100, 2);
        }
        return median with
        {
            Header = median.Header with { WorkloadId = workloadId },
            CpuCvPercent = Cv(results.Select(r => r.CpuScore)),
            MemoryCvPercent = Cv(results.Select(r => r.MemoryBandwidthGbs)),
        };
    }

    /// <summary>Per-metric median across trials (spec 83).</summary>
    public static SystemBenchmarkResult MedianOf(IEnumerable<SystemBenchmarkResult> results)
    {
        var list = results.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Sin resultados.", nameof(results));
        }

        double Median(Func<SystemBenchmarkResult, double> selector)
        {
            var values = list.Select(selector).OrderBy(value => value).ToList();
            var mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
        }

        var head = list[0];
        return head with
        {
            CpuScore = Median(result => result.CpuScore),
            MemoryBandwidthGbs = Median(result => result.MemoryBandwidthGbs),
            DiskReadMbs = Median(result => result.DiskReadMbs),
            DiskWriteMbs = Median(result => result.DiskWriteMbs),
            CpuMs = Median(result => result.CpuMs),
        };
    }

    public async Task<SystemBenchmarkResult> RunAsync(string workloadId = "system-baseline", CancellationToken ct = default)
    {
        var phases = new Dictionary<string, double>(StringComparer.Ordinal);
        var swTotal = Stopwatch.StartNew();
        var (cpuOps, cpuMs) = await Task.Run(() => MeasureCpu(ct), ct);
        phases["cpu"] = cpuMs / 1000d;
        var memSw = Stopwatch.StartNew();
        var memory = await Task.Run(() => MeasureMemoryBandwidth(ct), ct);
        memSw.Stop();
        phases["memory"] = memSw.Elapsed.TotalSeconds;
        var diskSw = Stopwatch.StartNew();
        var (read, write) = await Task.Run(() => MeasureDisk(ct), ct);
        diskSw.Stop();
        phases["disk"] = diskSw.Elapsed.TotalSeconds;
        swTotal.Stop();

        var header = new BenchmarkRunHeader(
            workloadId,
            DateTime.UtcNow,
            Environment.OSVersion.Version.Build,
            GpuDriverPlaceholder(),
            $"{Environment.ProcessorCount} logical processors",
            0,
            PowerState());

        return new SystemBenchmarkResult(header, cpuOps, memory, read, write, swTotal.Elapsed,
            CpuMs: cpuMs, PhaseSeconds: phases);
    }

    public static SystemBenchmarkComparison Compare(SystemBenchmarkResult baseline, SystemBenchmarkResult after)
    {
        if (baseline.CpuScore <= 0 || after.CpuScore <= 0)
        {
            return new(0, 0, "InsuficienteData");
        }

        var cpuDelta = PercentChange(baseline.CpuScore, after.CpuScore);
        var memoryDelta = PercentChange(baseline.MemoryBandwidthGbs, after.MemoryBandwidthGbs);

        var verdict =
            cpuDelta > NoiseFloorPercent || memoryDelta > NoiseFloorPercent ? "Mejora medible" :
            cpuDelta < -NoiseFloorPercent || memoryDelta < -NoiseFloorPercent ? "Regresión" :
            "Sin mejora medible";

        return new(cpuDelta, memoryDelta, verdict);
    }

    /// <summary>Comparación extendida incluyendo disco (no cambia el veredicto: CPU/memoria mandan).</summary>
    public static SystemBenchmarkFullComparison CompareFull(SystemBenchmarkResult baseline, SystemBenchmarkResult after)
    {
        var baseCmp = Compare(baseline, after);
        var diskReadDelta = PercentChange(baseline.DiskReadMbs, after.DiskReadMbs);
        var diskWriteDelta = PercentChange(baseline.DiskWriteMbs, after.DiskWriteMbs);
        return new(baseCmp.CpuDeltaPercent, baseCmp.MemoryDeltaPercent, diskReadDelta, diskWriteDelta, baseCmp.VerdictEs);
    }

    public static double PercentChange(double before, double after) =>
        before == 0 ? 0 : Math.Round((after - before) / before * 100, 2);

    private static (double OpsPerSecond, double Milliseconds) MeasureCpu(CancellationToken ct)
    {
        const int bound = 300_000;
        var sw = Stopwatch.StartNew();
        var total = 0L;
        Parallel.For(2, bound, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, () => 0L,
            (candidate, _, local) =>
            {
                ct.ThrowIfCancellationRequested();
                var isPrime = true;
                for (var divisor = 2; (long)divisor * divisor <= candidate; divisor++)
                {
                    if (candidate % divisor == 0) { isPrime = false; break; }
                }
                return local + (isPrime ? 1 : 0);
            },
            local => Interlocked.Add(ref total, local));
        sw.Stop();
        return (total / sw.Elapsed.TotalSeconds, sw.Elapsed.TotalMilliseconds);
    }

    private static readonly byte[] SharedSource = new byte[32 * 1024 * 1024];
    private static readonly byte[] SharedTarget = new byte[32 * 1024 * 1024];
    private static bool _memoryWarmedUp;

    private static double MeasureMemoryBandwidth(CancellationToken ct)
    {
        const int size = 32 * 1024 * 1024;
        if (!_memoryWarmedUp)
        {
            Random.Shared.NextBytes(SharedSource);
            Array.Copy(SharedSource, SharedTarget, size); // warmup fuera de crono
            _memoryWarmedUp = true;
        }
        var noGc = false;
        try { noGc = GC.TryStartNoGCRegion(128L * 1024 * 1024, disallowFullBlockingGC: true); } catch { }
        var sw = Stopwatch.StartNew();
        try
        {
            for (var round = 0; round < 4; round++)
            {
                ct.ThrowIfCancellationRequested();
                Array.Copy(SharedSource, SharedTarget, size);
            }
        }
        finally
        {
            sw.Stop();
            if (noGc) { try { GC.EndNoGCRegion(); } catch { } }
        }
        var totalBytes = 4L * size * 2 / (1024d * 1024 * 1024);
        return totalBytes / sw.Elapsed.TotalSeconds;
    }

    private static (double ReadMbs, double WriteMbs) MeasureDisk(CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cao-bench-{Guid.NewGuid():N}.tmp");
        try
        {
            const int bufferSize = 1024 * 1024;
            const int blocks = 64; // 64 MB
            var buffer = new byte[bufferSize];
            Random.Shared.NextBytes(buffer);

            double writeMbs, readMbs;
            var sw = Stopwatch.StartNew();
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize, FileOptions.SequentialScan))
            {
                for (var block = 0; block < blocks; block++)
                {
                    ct.ThrowIfCancellationRequested();
                    stream.Write(buffer, 0, buffer.Length);
                }
                stream.Flush(true);
                sw.Stop();
                writeMbs = blocks / sw.Elapsed.TotalSeconds;
            }
            using var verifyStream = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
            sw.Restart();
            long totalRead = 0;
            while (totalRead < (long)blocks * bufferSize)
            {
                ct.ThrowIfCancellationRequested();
                totalRead += RandomAccess.Read(verifyStream, buffer, totalRead);
            }
            sw.Stop();

            readMbs = (totalRead / (1024d * 1024)) / sw.Elapsed.TotalSeconds;
            GC.KeepAlive(buffer);
            return (readMbs, writeMbs);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    private static string PowerState() =>
        SystemPowerStatus.IsOnBattery() ? "battery" : "ac";

    private static string GpuDriverPlaceholder() => string.Empty;
}

internal static class SystemPowerStatus
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(ref POWER_SYSTEM_STATUS status);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POWER_SYSTEM_STATUS
    {
        public byte ACLineStatus;      // 0 offline, 1 online, 255 unknown
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte Reserved0;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    public static bool IsOnBattery()
    {
        try
        {
            var status = new POWER_SYSTEM_STATUS();
            return GetSystemPowerStatus(ref status) && status.ACLineStatus == 0;
        }
        catch
        {
            return false;
        }
    }
}
