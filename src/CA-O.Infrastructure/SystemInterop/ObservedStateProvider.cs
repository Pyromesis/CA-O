using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Performance;
using System.Management;
using Catalog = CAO.Core.Catalog.OptimizationCatalog;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>
/// Reads live state from external tools so optimizations can Detect/Capture
/// accurately: powercfg scheme, bcdedit hypervisor type, netsh autotuning,
/// hibernation availability, service start types.
/// </summary>
public sealed class ObservedStateProvider
{
    private readonly IProcessRunner _process;
    private readonly IServiceManager _services;

    public ObservedStateProvider(IProcessRunner process, IServiceManager services)
    {
        _process = process;
        _services = services;
    }

    public async Task WireAsync(CancellationToken ct = default)
    {
        // Power plan
        var (_, schemeOut) = await _process.RunAsync("powercfg", "/getactivescheme", ct);
        var guidMatch = Regex.Match(schemeOut, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
        Catalog.MaximumPowerPlan.ActiveSchemeGuid = guidMatch.Success ? guidMatch.Groups[1].Value : null;

        // Hypervisor launch type
        var (_, bcdOut) = await _process.RunAsync("bcdedit", "/enum {current}", ct);
        var launchMatch = Regex.Match(bcdOut, @"hypervisorlaunchtype\s+(\w+)");
        Catalog.DisableVbs.CurrentLaunchType = launchMatch.Success ? launchMatch.Groups[1].Value : null;

        // TCP autotuning level
        var (_, netshOut) = await _process.RunAsync("netsh", "int tcp show global", ct);
        var tuningMatch = Regex.Match(netshOut, @"(?:Auto-Tuning Level|nivel de autoajuste de recepción)\s*:\s*(\w+)", RegexOptions.IgnoreCase);
        Catalog.NormalizeTcpAutoTuning.CurrentLevel = tuningMatch.Success ? tuningMatch.Groups[1].Value : "normal";

        // Hibernation availability
        var (_, powerOut) = await _process.RunAsync("powercfg", "/a", ct);
        Catalog.DisableHibernate.HibernateAvailable =
            powerOut.Contains("Hibernation", StringComparison.OrdinalIgnoreCase) ||
            powerOut.Contains("Hibernación", StringComparison.OrdinalIgnoreCase) ||
            powerOut.Contains("Hibernacion", StringComparison.OrdinalIgnoreCase);

        // System disk media type
        Catalog.OptimizeSystemDrive.SystemDiskMediaType = await GetSystemDiskMediaTypeAsync(ct);

        // Service-backed optimizations
        var indexing = Catalog.DisableSearchIndexing;
        if (indexing is IServiceAwareOptimization aware)
        {
            aware.SetObservedStartType(_services.GetStartType(DisableSearchIndexing.ServiceName));
        }

        // OneDrive Run entry
        Catalog.DisableOneDriveAutostart.ObservedRunValue = null; // Detect reads live registry directly.
    }

    /// <summary>MSFT_PhysicalDisk media type of the disk containing the OS ("SSD"/"HDD"/"Unspecified").</summary>
    public async Task<string> GetSystemDiskMediaTypeAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var diskSearcher = new ManagementObjectSearcher(
                    "SELECT DeviceId FROM Win32_LogicalDisk WHERE DeviceID='C:'");
                using var partitionSearcher = new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='C:'} WHERE ResultClass=Win32_DiskPartition");
                foreach (var partition in partitionSearcher.Get())
                {
                    using var physicalSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceId"]}'}} WHERE ResultClass=Win32_DiskDrive");
                    foreach (var drive in physicalSearcher.Get())
                    {
                        var index = Convert.ToInt32(drive["Index"] ?? -1);
                        using var physicalDiskSearcher = new ManagementObjectSearcher(
                            @"root\Microsoft\Windows\Storage",
                            $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId='{index}'");
                        foreach (var physical in physicalDiskSearcher.Get())
                        {
                            // 3 = HDD, 4 = SSD
                            return Convert.ToInt32(physical["MediaType"]) == 3 ? "HDD" : "SSD";
                        }
                    }
                }
            }
            catch { /* fall through */ }
            return "SSD";
        }, ct);
    }
}
