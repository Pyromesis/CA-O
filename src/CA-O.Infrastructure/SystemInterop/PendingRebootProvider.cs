using Microsoft.Win32;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>
/// Detects pending reboot state (spec 26/§26 dashboard): Windows Update,
/// Component Based Servicing and file-rename operations.
/// </summary>
public sealed class PendingRebootProvider
{
    public PendingRebootReport Check()
    {
        var reasons = new List<string>();

        if (KeyHasValues(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
        {
            reasons.Add("Windows Update");
        }

        if (KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
        {
            reasons.Add("Component Based Servicing");
        }

        var fileRename = GetPendingFileRenameEntries();
        if (fileRename.Count > 0)
        {
            // Filtrar entradas benignas conocidas (EdgeUpdate, TEMP) que dejan la clave siempre presente
            // y solo reportar si queda al menos una entrada significativa tras el filtro.
            var significant = fileRename.Where(e =>
                !e.Contains("EdgeUpdate", StringComparison.OrdinalIgnoreCase) &&
                !e.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase) &&
                !e.StartsWith(@"\??\C:\Windows\Temp", StringComparison.OrdinalIgnoreCase)).ToList();
            // Si solo hay entradas benignas, no considerar como reinicio pendiente del sistema
            if (significant.Count > 0)
                reasons.Add("File rename pendiente");
            else if (fileRename.Count > 5)
                reasons.Add("File rename pendiente (múltiples operaciones)");
            // Si solo hay 1-5 entradas benignas, silenciar para evitar banner permanente
        }

        return new PendingRebootReport(reasons.Count > 0, reasons);
    }

    private static bool KeyExists(string path)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path);
        return key is not null;
    }

    private static IReadOnlyList<string> GetPendingFileRenameEntries()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            if (key is null) return Array.Empty<string>();
            var value = key.GetValue("PendingFileRenameOperations");
            if (value is string[] arr)
                return arr.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (value is string s && !string.IsNullOrWhiteSpace(s))
                return new[] { s };
            return Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }

    private static bool KeyHasValues(string path, string? valueName = null)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path);
        if (key is null) return false;
        if (valueName is not null) return key.GetValue(valueName) is not null;
        return key.GetValueNames().Length > 0 || key.GetSubKeyNames().Length > 0;
    }
}

/// <summary>Pending reboot facts for the dashboard.</summary>
public sealed record PendingRebootReport(bool Pending, IReadOnlyList<string> Reasons);
