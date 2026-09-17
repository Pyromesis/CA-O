using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CAO.Core.Abstractions;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace CAO.Infrastructure.Windows.SystemRegistry;

/// <summary>
/// Real-registry implementation of IRegistryAccessor with EXACT kind
/// fidelity (FASE 8): reads use DoNotExpandEnvironmentNames so REG_EXPAND_SZ
/// is captured unexpanded; writes map the declared kind verbatim.
///
/// HKCU se abre con RegOpenCurrentUser (no con el handle cacheado del
/// proceso): bajo suplantación (servicio SYSTEM ejecutando como el llamante)
/// lee/escribe el hive DEL USUARIO; sin suplantar conserva el
/// comportamiento anterior (hive del proceso).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryAccessor : IRegistryAccessor
{
    private static RegistryKey OpenBase(RegistryHive2 hive, bool writable, out bool owned)
    {
        if (hive == RegistryHive2.CurrentUser) return OpenCurrentUser(writable, out owned);
        // HKLM: handle estático Registry.LocalMachine — NUNCA disponerlo.
        // El llamante solo dispone la subclave (OpenSubKey/CreateSubKey),
        // nunca la base.
        owned = false;
        return Registry.LocalMachine;
    }

    private static RegistryKey OpenCurrentUser(bool writable, out bool owned)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                const int KeyQueryValue = 0x0001;
                const int KeySetValue = 0x0002;
                const int KeyCreateSubKey = 0x0004;
                const int KeyEnumerateSubKeys = 0x0008;
                const int KeyNotify = 0x0010;
                const int ReadControl = 0x00020000;
                var access = ReadControl | KeyQueryValue | KeyEnumerateSubKeys | KeyNotify
                    | (writable ? (KeySetValue | KeyCreateSubKey) : 0);
                if (RegOpenCurrentUser(access, out var hkey) == 0 && hkey != nint.Zero)
                {
                    owned = true;
                    return RegistryKey.FromHandle(new SafeRegistryHandle(hkey, ownsHandle: true));
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                // Fallback al comportamiento anterior.
            }
        }
        // Fallback: handle estático del proceso — tampoco se dispone.
        owned = false;
        return Registry.CurrentUser;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegOpenCurrentUser(int desiredAccess, out nint phkResult);

    public RegistryValueKind2 GetKind(RegistryHive2 hive, string keyPath, string valueName)
    {
        ValidatePath(hive, keyPath, valueName);
        var baseKey = OpenBase(hive, writable: false, out var ownsBase);
        try
        {
            using var key = baseKey.OpenSubKey(keyPath);
            var kind = key?.GetValueKind(valueName);
            return MapFromWin(kind);
        }
        finally { if (ownsBase) baseKey.Dispose(); }
    }

    public object? GetValue(RegistryHive2 hive, string keyPath, string valueName)
    {
        ValidatePath(hive, keyPath, valueName);
        var baseKey = OpenBase(hive, writable: false, out var ownsBase);
        try
        {
            using var key = baseKey.OpenSubKey(keyPath);
            return key?.GetValue(valueName);
        }
        finally { if (ownsBase) baseKey.Dispose(); }
    }

    public object? GetValueRaw(RegistryHive2 hive, string keyPath, string valueName, out RegistryValueKind2 kind)
    {
        ValidatePath(hive, keyPath, valueName);
        var baseKey = OpenBase(hive, writable: false, out var ownsBase);
        try
        {
            using var key = baseKey.OpenSubKey(keyPath);
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
        finally { if (ownsBase) baseKey.Dispose(); }
    }

    public void SetValue(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
        SetValueRaw(hive, keyPath, valueName, value, kind);

    public void SetValueRaw(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind)
    {
        ValidatePath(hive, keyPath, valueName);
        var baseKey = OpenBase(hive, writable: true, out var ownsBase);
        try
        {
            using var key = baseKey.CreateSubKey(keyPath, writable: true)!;
            key.SetValue(valueName, Coerce(value), MapToWin(kind));
        }
        finally { if (ownsBase) baseKey.Dispose(); }
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
        ValidatePath(hive, keyPath, valueName);
        var baseKey = OpenBase(hive, writable: true, out var ownsBase);
        try
        {
            using var key = baseKey.OpenSubKey(keyPath, writable: true);
            if (key is null || key.GetValue(valueName) is null) return false;
            key.DeleteValue(valueName, throwOnMissingValue: false);
            return true;
        }
        finally { if (ownsBase) baseKey.Dispose(); }
    }

    public IReadOnlyList<string> GetValueNames(RegistryHive2 hive, string keyPath)
    {
        ValidateKeyPath(hive, keyPath);
        var baseKey = OpenBase(hive, writable: false, out var ownsBase);
        try
        {
            using var key = baseKey.OpenSubKey(keyPath);
            return key?.GetValueNames() ?? Array.Empty<string>();
        }
        finally { if (ownsBase) baseKey.Dispose(); }
    }

    public IReadOnlyList<string> GetSubKeyNames(RegistryHive2 hive, string keyPath)
    {
        ValidateKeyPath(hive, keyPath);
        var baseKey = OpenBase(hive, writable: false, out var ownsBase);
        try
        {
            using var key = baseKey.OpenSubKey(keyPath);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        finally { if (ownsBase) baseKey.Dispose(); }
    }

    private static void ValidateKeyPath(RegistryHive2 hive, string keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath) || keyPath.Contains("..") || keyPath.Length > 512)
            throw new ArgumentException("Registry path inválido.", nameof(keyPath));
        if (hive is not (RegistryHive2.CurrentUser or RegistryHive2.LocalMachine))
            throw new UnauthorizedAccessException("Hive no permitido.");
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
