using System.Management;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

public sealed class ThermalDiagnosticsProvider
{
    public async Task<ThermalDiagnosticsReport> MeasureAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var zones = new List<ThermalZoneDiagnostic>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    new ManagementScope(@"root\WMI"),
                    new ObjectQuery("SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"));
                foreach (var zone in searcher.Get().Cast<ManagementObject>())
                {
                    ct.ThrowIfCancellationRequested();
                    var rawTemperature = zone["CurrentTemperature"];
                    var temperature = rawTemperature is null ? (double?)null : Convert.ToDouble(rawTemperature) / 10d - 273.15d;
                    zones.Add(new ThermalZoneDiagnostic(
                        zone["InstanceName"]?.ToString() ?? "ACPI thermal zone",
                        temperature,
                        "MSAcpi_ThermalZoneTemperature"));
                }
            }
            catch (ManagementException)
            {
                // Many modern systems do not expose ACPI thermal zones through WMI.
            }

            return new ThermalDiagnosticsReport(zones, zones.Count > 0, DateTime.UtcNow);
        }, ct);
    }
}