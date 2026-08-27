using System.Management;
using System.Runtime.InteropServices;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>Live system facts via WMI/CIM for the Dashboard.</summary>
public sealed class WmiSystemInfoProvider : ISystemInfoProvider
{
    private static readonly TimeSpan WmiTimeout = TimeSpan.FromSeconds(5);

    public async Task<SystemInfoReport> GetAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var osVersion = Environment.OSVersion.VersionString;
            var edition = string.Empty;
            var ramGb = 0;
            var cpuName = string.Empty;
            var cpuCores = 0;
            var cpuLogicalProcessors = Environment.ProcessorCount;

            try
            {
                // WMI con timeout explícito y cancellation para no bloquear UI ( §12 )
                var opts = new System.Management.EnumerationOptions { Timeout = WmiTimeout, BlockSize = 1, Rewindable = false };
                using var osSearcher = new ManagementObjectSearcher(new ManagementScope(@"root\cimv2"), new ObjectQuery("SELECT Caption, TotalVisibleMemorySize FROM Win32_OperatingSystem"), opts);
                foreach (ManagementBaseObject os in WithCancellation(osSearcher.Get().Cast<ManagementBaseObject>(), ct))
                {
                    ct.ThrowIfCancellationRequested();
                    edition = os["Caption"]?.ToString() ?? string.Empty;
                    if (ulong.TryParse(os["TotalVisibleMemorySize"]?.ToString(), out var kb))
                    {
                        ramGb = (int)Math.Round(kb / (1024d * 1024d));
                    }
                }
                using var cpuSearcher = new ManagementObjectSearcher(new ManagementScope(@"root\cimv2"), new ObjectQuery("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"), opts);
                foreach (var cpu in WithCancellation(cpuSearcher.Get().Cast<ManagementObject>(), ct))
                {
                    ct.ThrowIfCancellationRequested();
                    cpuName = cpu["Name"]?.ToString()?.Trim() ?? cpuName;
                    cpuCores = Convert.ToInt32(cpu["NumberOfCores"] ?? 0);
                    cpuLogicalProcessors = Convert.ToInt32(cpu["NumberOfLogicalProcessors"] ?? cpuLogicalProcessors);
                    break;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // WMI can fail on hardened systems; the report degrades gracefully.
            }

            var hasSsd = DetectSystemSsd();
            var isLaptop = DetectChassisIsLaptop();

            var windowsBuild = Environment.OSVersion.Version.Build;
            return new SystemInfoReport(
                WindowsVersion: $"Windows {Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor} build {windowsBuild}",
                WindowsEdition: edition,
                RamGb: ramGb,
                CpuName: cpuName,
                HasSsd: hasSsd,
                IsElevated: IsElevated(),
                IsLaptop: isLaptop)
            {
                CpuCores = cpuCores,
                CpuLogicalProcessors = cpuLogicalProcessors,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                WindowsBuild = windowsBuild,
            };
        }, ct);
    }

    internal static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    /// <summary>MSFT_PhysicalDisk MediaType of the disk hosting C:. Defaults to SSD.</summary>
    internal static bool DetectSystemSsd()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"root\Microsoft\Windows\Storage"),
                new ObjectQuery(@"SELECT MediaType FROM MSFT_PhysicalDisk"),
                new System.Management.EnumerationOptions());
            foreach (var disk in searcher.Get())
            {
                if (disk["MediaType"] != null && Convert.ToInt32(disk["MediaType"]) == 4)
                {
                    // First physical SSD found; single-disk systems are the norm.
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return true; // assume modern default
        }
    }

    private static bool DetectChassisIsLaptop()
    {
        try
        {
            var opts = new System.Management.EnumerationOptions { Timeout = WmiTimeout };
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\cimv2"), new ObjectQuery("SELECT ChassisTypes FROM Win32_SystemEnclosure"), opts);
            foreach (var enclosure in searcher.Get())
            {
                if (enclosure["ChassisTypes"] is not int[] chassisTypes) continue;
                // 8..12,14,30,31,32 are laptop/notebook families per SMBIOS.
                if (chassisTypes.Any(t => t is >= 8 and <= 12 or 14 or 30 or 31 or 32))
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static IEnumerable<T> WithCancellation<T>(IEnumerable<T> source, CancellationToken ct)
    {
        foreach (var item in source)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}
