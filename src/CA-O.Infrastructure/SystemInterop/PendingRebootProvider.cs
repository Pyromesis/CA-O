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

        if (KeyHasValues(@"SYSTEM\CurrentControlSet\Control\Session Manager", "PendingFileRenameOperations"))
        {
            reasons.Add("File rename pendiente");
        }

        return new PendingRebootReport(reasons.Count > 0, reasons);
    }

    private static bool KeyExists(string path)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path);
        return key is not null;
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
