using System.Management;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

public sealed class DriverDiagnosticsProvider
{
    private static readonly TimeSpan WmiTimeout = TimeSpan.FromSeconds(5);

    public async Task<DriverDiagnosticsReport> MeasureAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var drivers = new List<DriverDiagnostic>();
            try
            {
                var opts = new System.Management.EnumerationOptions { Timeout = WmiTimeout, BlockSize = 20, Rewindable = false };
                using var searcher = new ManagementObjectSearcher(
                    new System.Management.ManagementScope(@"root\cimv2"),
                    new System.Management.ObjectQuery("SELECT DeviceName, DeviceClass, Manufacturer, DriverVersion, DriverDate, IsSigned, Status, ConfigManagerErrorCode, DeviceID, HardwareID, InfName, DriverProviderName FROM Win32_PnPSignedDriver"),
                    opts);
                foreach (var device in searcher.Get().Cast<ManagementObject>())
                {
                    ct.ThrowIfCancellationRequested();
                    drivers.Add(new DriverDiagnostic(
                        device["DeviceName"]?.ToString() ?? string.Empty,
                        device["DeviceClass"]?.ToString() ?? string.Empty,
                        device["Manufacturer"]?.ToString() ?? string.Empty,
                        device["DriverVersion"]?.ToString() ?? string.Empty,
                        device["DriverDate"]?.ToString() ?? string.Empty,
                        device["IsSigned"] is bool signed ? signed : null,
                        device["Status"]?.ToString() ?? string.Empty,
                        Convert.ToInt32(device["ConfigManagerErrorCode"] ?? 0),
                        device["DeviceID"]?.ToString() ?? string.Empty,
                        (device["HardwareID"] as string[])?.FirstOrDefault() ?? string.Empty,
                        device["InfName"]?.ToString() ?? string.Empty,
                        device["DriverProviderName"]?.ToString() ?? string.Empty));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (ManagementException)
            {
                // WMI may be unavailable or restricted; return the partial report.
            }

            return new DriverDiagnosticsReport(drivers, DateTime.UtcNow);
        }, ct);
    }

    /// <summary>Identidad del equipo para buscar drivers originales del fabricante.</summary>
    public async Task<ComputerInfo> GetComputerInfoAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            string manufacturer = string.Empty, model = string.Empty, serial = string.Empty;
            try
            {
                var opts = new System.Management.EnumerationOptions { Timeout = WmiTimeout, BlockSize = 20, Rewindable = false };
                using var system = new ManagementObjectSearcher(
                    new System.Management.ManagementScope(@"root\cimv2"),
                    new System.Management.ObjectQuery("SELECT Manufacturer, Model FROM Win32_ComputerSystem"),
                    opts);
                foreach (var item in system.Get().Cast<ManagementObject>())
                {
                    manufacturer = item["Manufacturer"]?.ToString() ?? string.Empty;
                    model = item["Model"]?.ToString() ?? string.Empty;
                    break;
                }
                using var bios = new ManagementObjectSearcher(
                    new System.Management.ManagementScope(@"root\cimv2"),
                    new System.Management.ObjectQuery("SELECT SerialNumber FROM Win32_BIOS"),
                    opts);
                foreach (var item in bios.Get().Cast<ManagementObject>())
                {
                    serial = item["SerialNumber"]?.ToString() ?? string.Empty;
                    break;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (ManagementException) { }
            ct.ThrowIfCancellationRequested();
            return new ComputerInfo(manufacturer.Trim(), model.Trim(), serial.Trim());
        }, ct);
    }
}