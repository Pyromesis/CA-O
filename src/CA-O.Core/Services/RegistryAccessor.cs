using CAO.Core.Abstractions;
using Microsoft.Win32;

namespace CAO.Core.Services;

/// <summary>Real-registry implementation of IRegistryAccessor.</summary>
public sealed class RegistryAccessor : IRegistryAccessor
{
    private static RegistryKey OpenBase(RegistryHive2 hive) => hive switch
    {
        RegistryHive2.CurrentUser => Registry.CurrentUser,
        _ => Registry.LocalMachine,
    };

    public RegistryValueKind2 GetKind(RegistryHive2 hive, string keyPath, string valueName)
    {
        using var key = OpenBase(hive).OpenSubKey(keyPath);
        var kind = key?.GetValueKind(valueName);
        return kind == RegistryValueKind.String || kind == RegistryValueKind.ExpandString
            ? RegistryValueKind2.String
            : RegistryValueKind2.DWord;
    }

    public object? GetValue(RegistryHive2 hive, string keyPath, string valueName)
    {
        using var key = OpenBase(hive).OpenSubKey(keyPath);
        return key?.GetValue(valueName);
    }

    public void SetValue(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind)
    {
        using var key = OpenBase(hive).CreateSubKey(keyPath, writable: true)!;
        var winKind = kind == RegistryValueKind2.String ? RegistryValueKind.String : RegistryValueKind.DWord;
        if (value is int i && winKind == RegistryValueKind.QWord) { value = (long)i; winKind = RegistryValueKind.QWord; }
        key.SetValue(valueName, value, winKind);
    }

    public bool DeleteValue(RegistryHive2 hive, string keyPath, string valueName)
    {
        using var key = OpenBase(hive).OpenSubKey(keyPath, writable: true);
        if (key is null || key.GetValue(valueName) is null) return false;
        key.DeleteValue(valueName, throwOnMissingValue: false);
        return true;
    }

    public IReadOnlyList<string> GetValueNames(RegistryHive2 hive, string keyPath)
    {
        using var key = OpenBase(hive).OpenSubKey(keyPath);
        return key?.GetValueNames() ?? Array.Empty<string>();
    }
}
