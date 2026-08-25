using System.Management;
using Microsoft.Win32;
using CAO.Shared;

namespace CAO.Infrastructure.Input;

public sealed class InputDiagnosticsProvider
{
    public async Task<InputDiagnosticsReport> MeasureAsync(CancellationToken ct = default)
    {
        var acceleration = ReadMouseAcceleration();
        var hidDeviceCount = await CountHidDevicesAsync(ct);
        return new InputDiagnosticsReport(acceleration, hidDeviceCount, DateTime.UtcNow);
    }

    private static bool? ReadMouseAcceleration()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse");
            var speed = key?.GetValue("MouseSpeed")?.ToString();
            var threshold1 = key?.GetValue("MouseThreshold1")?.ToString();
            var threshold2 = key?.GetValue("MouseThreshold2")?.ToString();
            if (speed is null || threshold1 is null || threshold2 is null) return null;
            return speed == "1" && (threshold1 != "0" || threshold2 != "0");
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int> CountHidDevicesAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT DeviceID FROM Win32_PnPEntity WHERE PNPClass = 'HIDClass'");
                var count = 0;
                foreach (var _ in searcher.Get())
                {
                    ct.ThrowIfCancellationRequested();
                    count++;
                }
                return count;
            }
            catch (ManagementException)
            {
                return 0;
            }
        }, ct);
    }
}