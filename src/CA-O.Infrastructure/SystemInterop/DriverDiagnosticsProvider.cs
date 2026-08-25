using System.Management;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

public sealed class DriverDiagnosticsProvider
{
    public async Task<DriverDiagnosticsReport> MeasureAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var drivers = new List<DriverDiagnostic>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT DeviceName, DeviceClass, Manufacturer, DriverVersion, DriverDate, IsSigned, Status, ConfigManagerErrorCode FROM Win32_PnPSignedDriver");
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
                        Convert.ToInt32(device["ConfigManagerErrorCode"] ?? 0)));
                }
            }
            catch (ManagementException)
            {
                // WMI may be unavailable or restricted; return the partial report.
            }

            return new DriverDiagnosticsReport(drivers, DateTime.UtcNow);
        }, ct);
    }
}