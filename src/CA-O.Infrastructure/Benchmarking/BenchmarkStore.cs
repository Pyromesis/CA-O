using System.Security.Cryptography;
using System.Text;

namespace CAO.Infrastructure.Benchmarking;

/// <summary>Identidad máquina no reversible (solo comparación).</summary>
public static class MachineId
{
    public static string Current()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string ?? string.Empty;
        }
        catch { return string.Empty; }
    }
}

/// <summary>
/// Persistencia y validez de líneas base / sesiones A/B (base mínima Task 2:
/// identidad + hash; Task 3 añade TTL, paths y sesiones).
/// </summary>
public static class BenchmarkStore
{
    /// <summary>SHA256 truncado a 16 hex de MachineGuid+CPU: estable, no reversible.</summary>
    public static string ComputeMachineHash(string machineGuid, string cpuName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"CA-O|{machineGuid}|{cpuName}"));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
