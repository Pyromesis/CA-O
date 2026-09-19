using System.Management;

namespace CAO.Infrastructure.Benchmarking;

public sealed record BootInfo(DateTime BootTimeUtc, TimeSpan Uptime);

/// <summary>Arranque del SO vía WMI (Win32_OperatingSystem.LastBootUpTime,
/// formato WMI <c>yyyyMMddHHmmss.ffffff+UUU</c>). Null si WMI no disponible.</summary>
public static class BootInfoProvider
{
    public static BootInfo? GetBootInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            foreach (var os in searcher.Get())
            {
                var raw = os["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrEmpty(raw))
                {
                    var boot = ManagementDateTimeConverter.ToDateTime(raw).ToUniversalTime();
                    if (boot <= DateTime.UtcNow)
                        return new(boot, DateTime.UtcNow - boot);
                }
            }
        }
        catch { }
        return null;
    }
}
