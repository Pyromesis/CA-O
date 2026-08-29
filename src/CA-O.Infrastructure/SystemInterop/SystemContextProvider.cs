using System.Management;
using CAO.Core.Abstractions;
using CAO.Core.Gaming;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Security;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>
/// Aggregates measured diagnostics into the SystemContext consumed by
/// preconditions, recommendation buckets and profile gating. Everything here
/// is read-only observation; unknown facts stay null/false rather than being
/// invented. Implements responsible caching (Fase 48) with TTL/invalidation.
/// </summary>
public sealed class SystemContextProvider : ISystemContextProvider
{
    private readonly WmiSystemInfoProvider _systemInfo;
    private readonly SecurityDiagnosticsProvider _security;
    private readonly AntiCheatScanProvider _antiCheats;
    private readonly ThermalDiagnosticsProvider _thermals;
    private readonly GameDetectionProvider _games;
    private readonly PendingRebootProvider _pendingReboot;
    private readonly SystemContextCache _cache = new();

    public SystemContextProvider(
        WmiSystemInfoProvider? systemInfo = null,
        SecurityDiagnosticsProvider? security = null,
        AntiCheatScanProvider? antiCheats = null,
        ThermalDiagnosticsProvider? thermals = null,
        GameDetectionProvider? games = null,
        PendingRebootProvider? pendingReboot = null)
    {
        _systemInfo = systemInfo ?? new WmiSystemInfoProvider();
        _security = security ?? new SecurityDiagnosticsProvider();
        _antiCheats = antiCheats ?? new AntiCheatScanProvider();
        _thermals = thermals ?? new ThermalDiagnosticsProvider();
        _pendingReboot = pendingReboot ?? new PendingRebootProvider();
        _games = games ?? new GameDetectionProvider();
    }

    public void InvalidateCache() => _cache.Invalidate();

    public async Task<SystemContext> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGet(out var cached) && cached is not null)
        {
            return cached;
        }

        // Cada proveedor se protege individualmente: un fallo de WMI no debe dejar sin contexto
        SystemInfoReport info;
        try { info = await _systemInfo.GetAsync(ct); } catch { info = FallbackSystemInfo(); }

        SecurityDiagnosticsReport securityReport;
        try { securityReport = _security.Measure(); } catch { securityReport = new SecurityDiagnosticsReport(Array.Empty<SecurityFeatureState>(), false, DateTime.UtcNow); }

        IReadOnlyList<AntiCheatInfo> antiCheats;
        try { antiCheats = _antiCheats.Scan(); } catch { antiCheats = Array.Empty<AntiCheatInfo>(); }

        ThermalDiagnosticsReport thermal;
        try { thermal = await _thermals.MeasureAsync(ct); } catch { thermal = new ThermalDiagnosticsReport(Array.Empty<ThermalZoneDiagnostic>(), false, DateTime.UtcNow); }

        var gpu = QueryPrimaryGpu();
        bool onBattery;
        try { onBattery = QueryOnBattery(); } catch { onBattery = false; }

        IReadOnlyList<DetectedGame> games;
        try { games = await _games.DetectAsync(ct); } catch { games = Array.Empty<DetectedGame>(); }

        var secureBoot = securityReport.Features.FirstOrDefault(feature => feature.Name == "Secure Boot")?.Enabled;
        var vbs = securityReport.Features.FirstOrDefault(feature => feature.Name == "VBS")?.Enabled;
        var hvci = securityReport.Features.FirstOrDefault(feature => feature.Name == "HVCI")?.Enabled;

        bool? tpmPresent;
        try { tpmPresent = QueryTpmPresent(); } catch { tpmPresent = null; }

        PendingRebootReport pendingReport;
        try { pendingReport = _pendingReboot.Check(); } catch { pendingReport = new PendingRebootReport(false, Array.Empty<string>()); }

        var context = new SystemContext
        {
            WindowsBuild = info.WindowsBuild,
            WindowsEdition = info.WindowsEdition,
            Architecture = info.Architecture,
            ServicingState = info.WindowsBuild >= 22000 ? WindowsServicingState.Supported : WindowsServicingState.Unknown,
            CpuName = string.IsNullOrWhiteSpace(info.CpuName) ? FallbackCpuName() : info.CpuName,
            CpuCores = info.CpuCores == 0 ? Environment.ProcessorCount : info.CpuCores,
            CpuLogicalProcessors = info.CpuLogicalProcessors == 0 ? Environment.ProcessorCount : info.CpuLogicalProcessors,
            RamGb = info.RamGb == 0 ? FallbackRamGb() : info.RamGb,
            HasSsd = info.HasSsd,
            IsLaptop = info.IsLaptop,
            OnBattery = onBattery,
            GpuName = gpu?.Name ?? string.Empty,
            GpuVendor = DetectVendor(gpu?.Name),
            GpuDriverVersion = gpu?.DriverVersion ?? string.Empty,
            DisplayRefreshHz = gpu?.RefreshHz ?? 0,
            SecureBootEnabled = secureBoot,
            TpmPresent = tpmPresent,
            VbsEnabled = vbs,
            HvciEnabled = hvci,
            AntiCheats = antiCheats,
            GamesDetected = games.Select(game => game.Name).ToList(),
            KernelProtectedGameRunning = games.Any(detected =>
                CAO.Core.Gaming.GameProfileCatalog.All.FirstOrDefault(profile =>
                    profile.DisplayName == detected.Name) is { } gp && gp.IsKernelProtected()),
            PendingReboot = pendingReport.Pending,
            PendingRebootReasons = pendingReport.Reasons,
            ThermalState = MapThermal(thermal),
            MeasuredUtc = DateTime.UtcNow,
        };
        _cache.Set(context);
        return context;
    }

    private static SystemInfoReport FallbackSystemInfo()
    {
        var ramGb = FallbackRamGb();
        return new SystemInfoReport(
            WindowsVersion: Environment.OSVersion.VersionString,
            WindowsEdition: Environment.OSVersion.VersionString,
            RamGb: ramGb,
            CpuName: FallbackCpuName(),
            HasSsd: true,
            IsElevated: WmiSystemInfoProvider.IsElevated(),
            IsLaptop: false)
        {
            CpuCores = Environment.ProcessorCount,
            CpuLogicalProcessors = Environment.ProcessorCount,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            WindowsBuild = Environment.OSVersion.Version.Build,
        };
    }

    private static string FallbackCpuName()
    {
        try { return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "CPU no detectada (WMI no disponible)"; }
        catch { return "CPU no detectada"; }
    }

    private static int FallbackRamGb()
    {
        try
        {
            var mem = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            return (int)Math.Round(mem / (1024d * 1024 * 1024));
        }
        catch { return 0; }
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
        return max switch
        {
            >= 95 => ThermalState.Throttling,
            >= 85 => ThermalState.Warm,
            > 0 => ThermalState.Nominal,
            _ => ThermalState.Unknown,
        };
    }
}
