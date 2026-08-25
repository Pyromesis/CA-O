using Microsoft.Win32;
using CAO.Shared;

namespace CAO.Infrastructure.Security;

public sealed class SecurityDiagnosticsProvider
{
    private const string DeviceGuardPath = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
    private const string HvciPath = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
    private const string LsaPath = @"SYSTEM\CurrentControlSet\Control\Lsa";
    private const string SecureBootPath = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";

    public SecurityDiagnosticsReport Measure()
    {
        var features = new List<SecurityFeatureState>
        {
            ReadDwordFeature("Secure Boot", SecureBootPath, "UEFISecureBootEnabled"),
            ReadDwordFeature("VBS", DeviceGuardPath, "EnableVirtualizationBasedSecurity"),
            ReadDwordFeature("HVCI", HvciPath, "Enabled"),
            ReadDwordFeature("Credential Guard", LsaPath, "LsaCfgFlags"),
            ReadServiceFeature("Microsoft Defender", @"SYSTEM\CurrentControlSet\Services\WinDefend"),
        };

        var vanguardDetected = ServiceExists("vgc") || ServiceExists("vgk");
        return new SecurityDiagnosticsReport(features, vanguardDetected, DateTime.UtcNow);
    }

    private static SecurityFeatureState ReadDwordFeature(string name, string path, string valueName, string? unknownEvidence = null)
    {
        try
        {
            var value = Registry.GetValue($"HKEY_LOCAL_MACHINE\\{path}", valueName, null);
            if (value is null) return new(name, null, unknownEvidence ?? "Valor no presente; estado desconocido.");
            return new(name, Convert.ToInt32(value) != 0, $"Registro HKLM\\{path}\\{valueName}.");
        }
        catch (Exception ex)
        {
            return new(name, null, $"No se pudo leer el estado: {ex.GetType().Name}.");
        }
    }

    private static SecurityFeatureState ReadServiceFeature(string name, string path)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return new(name, key is not null, $"Servicio registrado en HKLM\\{path}.");
        }
        catch (Exception ex)
        {
            return new(name, null, $"No se pudo leer el servicio: {ex.GetType().Name}.");
        }
    }

    private static bool ServiceExists(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}