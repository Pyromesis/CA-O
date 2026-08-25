using System.ServiceProcess;
using Microsoft.Win32;
using CAO.Core.Abstractions;

namespace CAO.Core.Services;

/// <summary>
/// Windows service manager. Start-type changes go through the SCM registry
/// (Services\{name}\Start) because ServiceController cannot change the start
/// mode; delayed-auto is preserved via the DelayedAutostart value.
/// </summary>
public sealed class ServiceManager : IServiceManager
{
    private const string ServicesKeyPath = @"SYSTEM\CurrentControlSet\Services";

    public bool Exists(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            var _ = sc.Status;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? GetStartType(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{ServicesKeyPath}\{serviceName}");
            if (key is null) return null;
            var start = Convert.ToInt32(key.GetValue("Start") ?? -1);
            return start switch
            {
                2 => Equals(key.GetValue("DelayedAutostart"), 1) ? "Automatic (Delayed)" : "Automatic",
                3 => "Manual",
                4 => "Disabled",
                _ => $"Unknown({start})",
            };
        }
        catch
        {
            return null;
        }
    }

    public void SetStartType(string serviceName, string startType)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"{ServicesKeyPath}\{serviceName}", writable: true)
            ?? throw new InvalidOperationException($"Service '{serviceName}' not found in registry.");

        // Preserve delayed-auto flag unless explicitly changing away from Automatic.
        if (!startType.StartsWith("Automatic", StringComparison.OrdinalIgnoreCase))
        {
            key.SetValue("DelayedAutostart", 0, RegistryValueKind.DWord);
        }
        else if (startType.Contains("Delayed", StringComparison.OrdinalIgnoreCase))
        {
            key.SetValue("DelayedAutostart", 1, RegistryValueKind.DWord);
        }

        key.SetValue("Start", startType switch
        {
            var s when s.StartsWith("Automatic", StringComparison.OrdinalIgnoreCase) => 2,
            "Manual" => 3,
            "Disabled" => 4,
            "Boot" => 0,
            "System" => 1,
            _ => throw new ArgumentException($"Unsupported start type '{startType}'."),
        }, RegistryValueKind.DWord);
    }

    public async Task StopAsync(string serviceName, CancellationToken ct = default)
    {
        using var sc = new ServiceController(serviceName);
        if (sc.Status == ServiceControllerStatus.Running)
        {
            await Task.Run(() =>
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }, ct);
        }
    }

    public async Task StartAsync(string serviceName, CancellationToken ct = default)
    {
        using var sc = new ServiceController(serviceName);
        if (sc.Status == ServiceControllerStatus.Stopped && sc.StartType != ServiceStartMode.Disabled)
        {
            await Task.Run(() =>
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }, ct);
        }
    }
}
