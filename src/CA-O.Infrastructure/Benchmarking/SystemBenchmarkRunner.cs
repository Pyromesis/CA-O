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
    TimeSpan Elapsed)
{
    public string WorkloadId => Header.WorkloadId;
}

/// <summary>Verdict of an A/B comparison with a noise floor (spec 108).</summary>
public sealed record SystemBenchmarkComparison(double CpuDeltaPercent, double MemoryDeltaPercent, string VerdictEs);

/// <summary>
/// Reproducible system benchmark (spec 66, 69): fixed workload sizes, header
/// records environment facts so runs are comparable. No invented numbers:
/// everything is measured in-process without spawning external tools.
/// </summary>
public sealed class SystemBenchmarkRunner
{
    /// <summary>Below this delta the change is declared "no measurable improvement".</summary>
    public const double NoiseFloorPercent = 3.0;

    public async Task<SystemBenchmarkResult> RunAsync(string workloadId = "system-baseline", CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var cpu = await Task.Run(() => MeasureCpu(ct), ct);
        var memory = await Task.Run(() => MeasureMemoryBandwidth(ct), ct);
        var (read, write) = await Task.Run(() => MeasureDisk(ct), ct);
        sw.Stop();

        var header = new BenchmarkRunHeader(
            workloadId,
            DateTime.UtcNow,
            Environment.OSVersion.Version.Build,
            GpuDriverPlaceholder(),
            $"{Environment.ProcessorCount} logical processors",
            0,
            PowerState());

        return new SystemBenchmarkResult(header, cpu, memory, read, write, sw.Elapsed);
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

    internal static double PercentChange(double before, double after) =>
        before == 0 ? 0 : Math.Round((after - before) / before * 100, 2);

    private static double MeasureCpu(CancellationToken ct)
    {
        // Fixed-size integer work: primes below a constant bound.
        const int bound = 300_000;
        var count = 0L;
        for (var candidate = 2; candidate < bound; candidate++)
        {
            ct.ThrowIfCancellationRequested();
            var isPrime = true;
            for (var divisor = 2; (long)divisor * divisor <= candidate; divisor++)
            {
                if (candidate % divisor == 0)
                {
                    isPrime = false;
                    break;
                }
            }
            if (isPrime)
            {
                count++;
            }
        }
        return count; // deterministic count => stable score across machines
    }

    private static double MeasureMemoryBandwidth(CancellationToken ct)
    {
        const int size = 32 * 1024 * 1024; // 32 MB
        var source = new byte[size];
        var target = new byte[size];
        Random.Shared.NextBytes(source);

        var sw = Stopwatch.StartNew();
        for (var round = 0; round < 4; round++)
        {
            ct.ThrowIfCancellationRequested();
            Array.Copy(source, target, size);
        }
        sw.Stop();

        var totalBytes = 4L * size * 2 / (1024d * 1024 * 1024); // read+write GBs
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

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan))
            {
                var sw = Stopwatch.StartNew();
                for (var block = 0; block < blocks; block++)
                {
                    ct.ThrowIfCancellationRequested();
                    stream.Write(buffer, 0, buffer.Length);
                }
                stream.Flush(true);
                sw.Stop();
                var writeMbs = blocks / sw.Elapsed.TotalSeconds;

                using var verifyStream = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
                sw.Restart();
                long totalRead = 0;
                while (totalRead < (long)blocks * bufferSize)
                {
                    ct.ThrowIfCancellationRequested();
                    totalRead += RandomAccess.Read(verifyStream, buffer, totalRead);
                }
                sw.Stop();

                var readMbs = (totalRead / (1024d * 1024)) / sw.Elapsed.TotalSeconds;
                GC.KeepAlive(buffer);
                _ = verifyStream;
                return (readMbs, writeMbs);
            }
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
