using CAO.Core.Abstractions;
using Microsoft.Win32;

namespace CAO.Infrastructure.Windows.SystemRegistry;

/// <summary>
/// Real-registry implementation of IRegistryAccessor with EXACT kind
/// fidelity (FASE 8): reads use DoNotExpandEnvironmentNames so REG_EXPAND_SZ
/// is captured unexpanded; writes map the declared kind verbatim.
/// </summary>
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
        return MapFromWin(kind);
    }

    public object? GetValue(RegistryHive2 hive, string keyPath, string valueName)
    {
        ValidatePath(hive, keyPath, valueName);
        using var key = OpenBase(hive).OpenSubKey(keyPath);
        return key?.GetValue(valueName);
    }

    public object? GetValueRaw(RegistryHive2 hive, string keyPath, string valueName, out RegistryValueKind2 kind)
    {
        using var key = OpenBase(hive).OpenSubKey(keyPath);
        if (key is null)
        {
            kind = RegistryValueKind2.None;
            return null;
        }

        try
        {
            var winKind = key.GetValueKind(valueName);
            kind = MapFromWin(winKind);
            // DoNotExpand: REG_EXPAND_SZ must round-trip with %VARS% intact.
            return key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        }
        catch (System.IO.IOException)
        {
            kind = RegistryValueKind2.None;
            return null;
        }
    }

    public void SetValue(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
        SetValueRaw(hive, keyPath, valueName, value, kind);

    public void SetValueRaw(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind)
    {
        ValidatePath(hive, keyPath, valueName);
        using var key = OpenBase(hive).CreateSubKey(keyPath, writable: true)!;
        key.SetValue(valueName, Coerce(value), MapToWin(kind));
    }

    private static void ValidatePath(RegistryHive2 hive, string keyPath, string valueName)
    {
        if (string.IsNullOrWhiteSpace(keyPath) || keyPath.Contains("..") || keyPath.Length > 512)
            throw new ArgumentException("Registry path inválido.", nameof(keyPath));
        if (string.IsNullOrWhiteSpace(valueName) || valueName.Length > 256)
            throw new ArgumentException("Registry value name inválido.", nameof(valueName));
        // Sólo HKLM/HKCU permitidos — nunca HKCR/HKCC
        if (hive is not (RegistryHive2.CurrentUser or RegistryHive2.LocalMachine))
            throw new UnauthorizedAccessException("Hive no permitido.");
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

    private static RegistryValueKind2 MapFromWin(RegistryValueKind? kind) => kind switch
    {
        RegistryValueKind.String => RegistryValueKind2.String,
        RegistryValueKind.ExpandString => RegistryValueKind2.ExpandString,
        RegistryValueKind.Binary => RegistryValueKind2.Binary,
        RegistryValueKind.DWord => RegistryValueKind2.DWord,
        RegistryValueKind.MultiString => RegistryValueKind2.MultiString,
        RegistryValueKind.QWord => RegistryValueKind2.QWord,
        _ => RegistryValueKind2.None,
    };

    private static RegistryValueKind MapToWin(RegistryValueKind2 kind) => kind switch
    {
        RegistryValueKind2.String => RegistryValueKind.String,
        RegistryValueKind2.ExpandString => RegistryValueKind.ExpandString,
        RegistryValueKind2.Binary => RegistryValueKind.Binary,
        RegistryValueKind2.MultiString => RegistryValueKind.MultiString,
        RegistryValueKind2.QWord => RegistryValueKind.QWord,
        RegistryValueKind2.DWord => RegistryValueKind.DWord,
        _ => RegistryValueKind.None,
    };

    private static object Coerce(object value) => value switch
    {
        int or long or string or byte[] or string[] => value,
        uint u => (long)u,
        _ => value.ToString() ?? string.Empty,
    };
}
