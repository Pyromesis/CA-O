using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CAO.Shared;

namespace CAO.Infrastructure.Benchmarking;

/// <summary>Sesión A/B ligada a una optimización (o "manual").</summary>
public sealed record BenchmarkSession(
    SystemBenchmarkResult Before,
    SystemBenchmarkResult? After,
    IReadOnlyList<double>? DnsBeforeMs,
    IReadOnlyList<double>? DnsAfterMs,
    string Category,
    string OptimizationId);

/// <summary>Identidad máquina no reversible (solo comparación).</summary>
public static class MachineId
{
    public static string Current()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string ?? string.Empty;
        }
        catch { return string.Empty; }
    }
}

public static class BenchmarkStore
{
    public static readonly TimeSpan BaselineTtl = TimeSpan.FromDays(7);

    /// <summary>SHA256 truncado a 16 hex de MachineGuid+CPU: estable, no reversible.</summary>
    public static string ComputeMachineHash(string machineGuid, string cpuName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"CA-O|{machineGuid}|{cpuName}"));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    public static string BaselinePath => Path.Combine(CaOPaths.BenchmarksDirectory, "baseline.json");

    public static string SessionPathFor(string optimizationId, DateTime utc)
    {
        var safe = string.Concat(optimizationId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        return Path.Combine(CaOPaths.BenchmarksDirectory, $"{safe}-{utc:yyyyMMddHHmmss}.json");
    }

    public static bool IsBaselineValid(SystemBenchmarkResult baseline, SystemBenchmarkResult current, DateTime utcNow)
    {
        if ((utcNow - baseline.Header.TimestampUtc) > BaselineTtl) return false;
        return string.Equals(baseline.Header.MachineHash, current.Header.MachineHash, StringComparison.Ordinal)
            && baseline.Header.WindowsBuild == current.Header.WindowsBuild
            && string.Equals(baseline.Header.OsUbr, current.Header.OsUbr, StringComparison.Ordinal)
            && string.Equals(baseline.Header.AppVersion, current.Header.AppVersion, StringComparison.Ordinal)
            && string.Equals(baseline.Header.PowerState, current.Header.PowerState, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task SaveJsonAsync<T>(string path, T value, CancellationToken ct)
    {
        // Escritura atómica (tmp + flush + move): un crash a mitad de
        // escritura nunca deja baseline.json truncado.
        Directory.CreateDirectory(CaOPaths.BenchmarksDirectory);
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value), ct);
        File.Move(tmp, path, overwrite: true);
    }

    public static async Task<T?> LoadJsonAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch { return default; } // fichero ausente o corrupto: sin datos, sin crash
    }

    /// <summary>Timeout adaptativo: 120 s base SSD, 300 s si el volumen sistema es HDD.</summary>
    public static TimeSpan TimeoutForSystemDrive()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (string.Equals(drive.Name, root, StringComparison.OrdinalIgnoreCase))
                    return drive.DriveType == DriveType.Fixed && IsHdd(root) ? TimeSpan.FromSeconds(300) : TimeSpan.FromSeconds(120);
            }
        }
        catch { }
        return TimeSpan.FromSeconds(120);
    }

    private static bool IsHdd(string root)
    {
        try
        {
            // Correlaciona el root con SU disco físico: LogicalDisk → Partition → DiskDrive.
            var deviceId = (Path.GetPathRoot(root) ?? root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(deviceId)) return true;
            var safeId = deviceId.Replace("'", string.Empty);
            using var logicalSearcher = new System.Management.ManagementObjectSearcher(
                $"SELECT DeviceID FROM Win32_LogicalDisk WHERE DeviceID='{safeId}'");
            foreach (System.Management.ManagementObject logical in logicalSearcher.Get())
            {
                using (logical)
                {
                    foreach (System.Management.ManagementObject partition in logical.GetRelated("Win32_DiskPartition"))
                    {
                        using (partition)
                        {
                            foreach (System.Management.ManagementObject drive in partition.GetRelated("Win32_DiskDrive"))
                            {
                                using (drive)
                                {
                                    var media = drive["MediaType"]?.ToString() ?? string.Empty;
                                    if (media.Contains("Fixed", StringComparison.OrdinalIgnoreCase)) return true;
                                }
                            }
                        }
                    }
                }
                return false; // volumen localizado en un disco físico no-HDD
            }
        }
        catch { }
        return true; // correlación imposible: fallback conservador (timeout largo)
    }
}
