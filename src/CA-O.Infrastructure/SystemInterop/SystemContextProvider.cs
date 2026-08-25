using System.Management;
using CAO.Core.Abstractions;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Security;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>
/// Aggregates measured diagnostics into the SystemContext consumed by
/// preconditions, recommendation buckets and profile gating. Everything here
/// is read-only observation; unknown facts stay null/false rather than being
/// invented.
/// </summary>
public sealed class SystemContextProvider : ISystemContextProvider
{
    private readonly WmiSystemInfoProvider _systemInfo;
    private readonly SecurityDiagnosticsProvider _security;
    private readonly AntiCheatScanProvider _antiCheats;
    private readonly ThermalDiagnosticsProvider _thermals;
    private readonly GameDetectionProvider _games;

    public SystemContextProvider(
        WmiSystemInfoProvider? systemInfo = null,
        SecurityDiagnosticsProvider? security = null,
        AntiCheatScanProvider? antiCheats = null,
        ThermalDiagnosticsProvider? thermals = null,
        GameDetectionProvider? games = null)
    {
        _systemInfo = systemInfo ?? new WmiSystemInfoProvider();
        _security = security ?? new SecurityDiagnosticsProvider();
        _antiCheats = antiCheats ?? new AntiCheatScanProvider();
        _thermals = thermals ?? new ThermalDiagnosticsProvider();
        _games = games ?? new GameDetectionProvider();
    }

    public async Task<SystemContext> GetAsync(CancellationToken ct = default)
    {
        var info = await _systemInfo.GetAsync(ct);
        var securityReport = _security.Measure();
        var antiCheats = _antiCheats.Scan();
        var thermal = await _thermals.MeasureAsync(ct);

        var gpu = QueryPrimaryGpu();
        var onBattery = QueryOnBattery();
        var games = await _games.DetectAsync(ct);

        var secureBoot = securityReport.Features.FirstOrDefault(feature => feature.Name == "Secure Boot")?.Enabled;
        var vbs = securityReport.Features.FirstOrDefault(feature => feature.Name == "VBS")?.Enabled;
        var hvci = securityReport.Features.FirstOrDefault(feature => feature.Name == "HVCI")?.Enabled;

        return new SystemContext
        {
            WindowsBuild = info.WindowsBuild,
            WindowsEdition = info.WindowsEdition,
            Architecture = info.Architecture,
            ServicingState = info.WindowsBuild >= 22000 ? WindowsServicingState.Supported : WindowsServicingState.Unknown,
            CpuName = info.CpuName,
            CpuCores = info.CpuCores,
            CpuLogicalProcessors = info.CpuLogicalProcessors,
            RamGb = info.RamGb,
            HasSsd = info.HasSsd,
            IsLaptop = info.IsLaptop,
            OnBattery = onBattery,
            GpuName = gpu?.Name ?? string.Empty,
            GpuVendor = DetectVendor(gpu?.Name),
            GpuDriverVersion = gpu?.DriverVersion ?? string.Empty,
            DisplayRefreshHz = gpu?.RefreshHz ?? 0,
            SecureBootEnabled = secureBoot,
            TpmPresent = QueryTpmPresent(),
            VbsEnabled = vbs,
            HvciEnabled = hvci,
            AntiCheats = antiCheats,
            GamesDetected = games.Select(game => game.Name).ToList(),
            ThermalState = MapThermal(thermal),
            MeasuredUtc = DateTime.UtcNow,
        };
    }

    private static (string Name, string DriverVersion, int RefreshHz)? QueryPrimaryGpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DriverVersion, CurrentRefreshRate FROM Win32_VideoController");
            ManagementObject? best = null;
            foreach (var adapter in searcher.Get().Cast<ManagementObject>())
            {
                if (best is null ||
                    Convert.ToInt32(adapter["AdapterRAM"] ?? best["AdapterRAM"] ?? 0) >
                    Convert.ToInt32(best["AdapterRAM"] ?? 0))
                {
                    best = adapter;
                }
            }

            if (best is null)
            {
                return null;
            }

            return (
                best["Name"]?.ToString() ?? string.Empty,
                best["DriverVersion"]?.ToString() ?? string.Empty,
                Convert.ToInt32(best["CurrentRefreshRate"] ?? 0));
        }
        catch
        {
            return null;
        }
    }

    private static string DetectVendor(string? gpuName) =>
        gpuName switch
        {
            null or "" => string.Empty,
            var name when name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) => "NVIDIA",
            var name when name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                          name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) => "AMD",
            var name when name.Contains("Intel", StringComparison.OrdinalIgnoreCase) => "Intel",
            _ => string.Empty,
        };

    private static bool QueryOnBattery()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT BatteryStatus FROM Win32_Battery");
            foreach (var battery in searcher.Get())
            {
                // BatteryStatus 1 == "Other"/discharging on AC-less laptops; 2 == "Unknown";
                // values 1-5 generally indicate not charging from AC.
                var status = Convert.ToInt32(battery["BatteryStatus"] ?? 2);
                return status is 1 or 3 or 4 or 5;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool? QueryTpmPresent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftTpm",
                "SELECT * FROM Win32_Tpm");
            return searcher.Get().Count > 0;
        }
        catch
        {
            return null;
        }
    }

    private static ThermalState MapThermal(ThermalDiagnosticsReport report)
    {
        if (!report.IsAvailable)
        {
            return ThermalState.Unknown;
        }

        var max = report.Zones.Select(zone => zone.TemperatureCelsius ?? 0).DefaultIfEmpty(0).Max();
        // Heuristic thresholds until vendor sensor APIs are wired; documented.
        return max switch
        {
            >= 95 => ThermalState.Throttling,
            >= 85 => ThermalState.Warm,
            > 0 => ThermalState.Nominal,
            _ => ThermalState.Unknown,
        };
    }
}
