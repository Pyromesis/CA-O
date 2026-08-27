using System.Management;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

public sealed class PerformanceDiagnosticsProvider
{
    private static readonly TimeSpan WmiTimeout = TimeSpan.FromSeconds(5);

    public async Task<PerformanceDiagnosticsReport> MeasureAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var processors = new List<ProcessorPerformance>();
            var graphicsAdapters = new List<GraphicsAdapterDiagnostic>();
            try
            {
                var opts = new System.Management.EnumerationOptions { Timeout = WmiTimeout, BlockSize = 10, Rewindable = false };
                using var processorSearcher = new ManagementObjectSearcher(
                    new System.Management.ManagementScope(@"root\cimv2"),
                    new System.Management.ObjectQuery("SELECT Name, LoadPercentage, CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor"),
                    opts);
                foreach (var processor in processorSearcher.Get().Cast<ManagementObject>())
                {
                    ct.ThrowIfCancellationRequested();
                    processors.Add(new ProcessorPerformance(
                        processor["Name"]?.ToString()?.Trim() ?? string.Empty,
                        ToNullableInt(processor["LoadPercentage"]),
                        ToNullableInt(processor["CurrentClockSpeed"]),
                        ToNullableInt(processor["MaxClockSpeed"])));
                }

                using var graphicsSearcher = new ManagementObjectSearcher(
                    new System.Management.ManagementScope(@"root\cimv2"),
                    new System.Management.ObjectQuery("SELECT Name, DriverVersion, AdapterRAM, Status FROM Win32_VideoController"),
                    opts);
                foreach (var adapter in graphicsSearcher.Get().Cast<ManagementObject>())
                {
                    ct.ThrowIfCancellationRequested();
                    graphicsAdapters.Add(new GraphicsAdapterDiagnostic(
                        adapter["Name"]?.ToString()?.Trim() ?? string.Empty,
                        adapter["DriverVersion"]?.ToString() ?? string.Empty,
                        ToNullableUlong(adapter["AdapterRAM"]),
                        adapter["Status"]?.ToString() ?? string.Empty));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (ManagementException)
            {
                // Return partial observations when WMI is restricted or unavailable.
            }

            return new PerformanceDiagnosticsReport(processors, graphicsAdapters, DateTime.UtcNow);
        }, ct);
    }

    private static int? ToNullableInt(object? value) =>
        value is null ? null : int.TryParse(value.ToString(), out var parsed) ? parsed : null;

    private static ulong? ToNullableUlong(object? value) =>
        value is null ? null : ulong.TryParse(value.ToString(), out var parsed) ? parsed : null;
}