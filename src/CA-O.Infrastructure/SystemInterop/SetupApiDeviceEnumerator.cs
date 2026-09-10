using System.Runtime.InteropServices;
using System.Text;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>
/// Un dispositivo tal como lo ve el Administrador de dispositivos (SetupAPI),
/// incluyendo los que WMI (Win32_PnPSignedDriver) jamás lista: sin controlador
/// (código 28), ocultos/desconectados y sin firma.
/// </summary>
internal sealed record SetupApiDevice(
    string InstanceId,
    string Description,
    string FriendlyName,
    string Manufacturer,
    string DeviceClass,
    string ClassGuid,
    string[] HardwareIds,
    string[] CompatibleIds,
    string Enumerator,
    string DriverKey,
    string DriverVersion,
    string DriverDate,
    string DriverProvider,
    string InfPath,
    int ProblemCode,
    bool IsPresent);

/// <summary>Fila WMI para enriquecer (versión/fecha/firma/INF/proveedor).</summary>
internal sealed record WmiDriverInfo(
    string DeviceId,
    string Name,
    string DeviceClass,
    string Manufacturer,
    string Version,
    string Date,
    bool? IsSigned,
    string Status,
    int ProblemCode,
    string HardwareId,
    string InfName,
    string Provider);

/// <summary>
/// Enumeración total de dispositivos vía SetupAPI, igual que el Administrador
/// de dispositivos: presentes (lo visible) + no presentes (lo "oculto").
/// Solo lectura; funciona sin elevación.
/// </summary>
internal static partial class SetupApiDeviceEnumerator
{
    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_ALLCLASSES = 0x00000004;

    private const uint SPDRP_DEVICEDESC = 0x00000000;
    private const uint SPDRP_HARDWAREID = 0x00000001;
    private const uint SPDRP_COMPATIBLEIDS = 0x00000002;
    private const uint SPDRP_CLASS = 0x00000007;
    private const uint SPDRP_CLASSGUID = 0x00000008;
    private const uint SPDRP_DRIVER = 0x00000009;
    private const uint SPDRP_MFG = 0x0000000B;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    private const uint SPDRP_ENUMERATOR_NAME = 0x00000016;

    private const int ERROR_NO_MORE_ITEMS = 259;
    private const int CR_SUCCESS = 0;

    private static readonly IntPtr InvalidHandle = new(-1);

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr SetupDiGetClassDevs(nint classGuid, string? enumerator, nint hwndParent, uint flags);

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiEnumDeviceInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceRegistryPropertyW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property,
        out uint propertyRegDataType, byte[]? propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInstanceIdW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetupDiGetDeviceInstanceId(
        IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        byte[]? deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiDestroyDeviceInfoList", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_DevNode_Status", SetLastError = false)]
    internal static partial int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SP_DEVINFO_DATA
    {
        public uint CbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public nint Reserved;
    }

    /// <summary>Todos los dispositivos: presentes + ocultos. Clave: InstanceId.</summary>
    public static IReadOnlyList<SetupApiDevice> EnumerateAll(CancellationToken ct = default)
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dev in Enumerate(DIGCF_ALLCLASSES | DIGCF_PRESENT, ct))
            present.Add(dev.InstanceId);

        var all = new List<SetupApiDevice>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dev in Enumerate(DIGCF_ALLCLASSES, ct))
        {
            ct.ThrowIfCancellationRequested();
            if (!seen.Add(dev.InstanceId)) continue;
            // Sin nombre no se puede mostrar ni accionar: fuera (la UI hace lo mismo).
            if (string.IsNullOrWhiteSpace(dev.Description) && string.IsNullOrWhiteSpace(dev.FriendlyName)) continue;
            all.Add(dev with { IsPresent = present.Contains(dev.InstanceId) });
        }
        return all;
    }

    private static List<SetupApiDevice> Enumerate(uint flags, CancellationToken ct)
    {
        var result = new List<SetupApiDevice>();
        var set = SetupDiGetClassDevs(nint.Zero, null, nint.Zero, flags);
        if (set == IntPtr.Zero || set == InvalidHandle) return result;
        try
        {
            uint index = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var data = new SP_DEVINFO_DATA { CbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                if (!SetupDiEnumDeviceInfo(set, index, ref data))
                {
                    if (Marshal.GetLastWin32Error() == ERROR_NO_MORE_ITEMS) break;
                    index++;
                    continue;
                }
                index++;
                try
                {
                    var dev = ReadDevice(set, ref data);
                    if (dev is not null) result.Add(dev);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* un dispositivo roto no tumba el inventario */ }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
        return result;
    }

    private static SetupApiDevice? ReadDevice(IntPtr set, ref SP_DEVINFO_DATA data)
    {
        var instanceId = GetInstanceId(set, ref data);
        if (string.IsNullOrWhiteSpace(instanceId)) return null;

        var desc = GetString(set, ref data, SPDRP_DEVICEDESC);
        var friendly = GetString(set, ref data, SPDRP_FRIENDLYNAME);
        var driverKey = GetString(set, ref data, SPDRP_DRIVER);
        var classGuid = GetString(set, ref data, SPDRP_CLASSGUID);

        var (drvVersion, drvDate, drvProvider, infPath) = ReadDriverKey(driverKey, classGuid);

        int problem = -1;
        try
        {
            if (CM_Get_DevNode_Status(out _, out var problemNumber, data.DevInst, 0) == CR_SUCCESS)
                problem = unchecked((int)problemNumber);
        }
        catch { }

        return new SetupApiDevice(
            instanceId,
            desc,
            friendly,
            GetString(set, ref data, SPDRP_MFG),
            GetString(set, ref data, SPDRP_CLASS),
            classGuid,
            GetMultiString(set, ref data, SPDRP_HARDWAREID),
            GetMultiString(set, ref data, SPDRP_COMPATIBLEIDS),
            GetString(set, ref data, SPDRP_ENUMERATOR_NAME),
            driverKey,
            drvVersion,
            drvDate,
            drvProvider,
            infPath,
            problem,
            IsPresent: true);
    }

    private static string GetInstanceId(IntPtr set, ref SP_DEVINFO_DATA data)
    {
        // Size query: lo normal es FALSE + ERROR_INSUFFICIENT_BUFFER con el tamaño.
        SetupDiGetDeviceInstanceId(set, ref data, null, 0, out var needed);
        if (needed <= 0 || needed > 512) return string.Empty;
        var buffer = new byte[needed * 2];
        if (!SetupDiGetDeviceInstanceId(set, ref data, buffer, needed, out _))
            return string.Empty;
        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    private static string GetString(IntPtr set, ref SP_DEVINFO_DATA data, uint property)
    {
        SetupDiGetDeviceRegistryProperty(set, ref data, property, out _, null, 0, out var needed);
        if (needed == 0 || needed > 65536) return string.Empty;
        // needed viene en bytes; margen para el terminador.
        var buffer = new byte[needed + 2];
        if (!SetupDiGetDeviceRegistryProperty(set, ref data, property, out var type, buffer, (uint)buffer.Length, out var used) || used == 0)
            return string.Empty;
        // REG_SZ / REG_EXPAND_SZ; si llega otro tipo, igual se intenta como texto.
        _ = type;
        return Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(used, (uint)buffer.Length)).TrimEnd('\0').Trim();
    }

    private static string[] GetMultiString(IntPtr set, ref SP_DEVINFO_DATA data, uint property)
    {
        SetupDiGetDeviceRegistryProperty(set, ref data, property, out _, null, 0, out var needed);
        if (needed == 0 || needed > 65536) return Array.Empty<string>();
        var buffer = new byte[needed + 2];
        if (!SetupDiGetDeviceRegistryProperty(set, ref data, property, out _, buffer, (uint)buffer.Length, out var used) || used == 0)
            return Array.Empty<string>();
        return SplitMultiString(Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(used, (uint)buffer.Length)));
    }

    /// <summary>Parte REG_MULTI_SZ en líneas, sin vacíos.</summary>
    public static string[] SplitMultiString(string raw) =>
        raw.Split('\0').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

    /// <summary>
    /// Detalle del controlador desde su clave (HKLM\...\Control\Class\{guid}\nnnn).
    /// La fecha del registro ("6-21-2006") se normaliza a formato CIM para que
    /// el formateo existente la pinte como dd/MM/aaaa.
    /// </summary>
    internal static (string Version, string Date, string Provider, string Inf) ReadDriverKey(string driverKey, string classGuid)
    {
        if (string.IsNullOrWhiteSpace(driverKey)) return (string.Empty, string.Empty, string.Empty, string.Empty);
        var relative = driverKey.StartsWith('{')
            ? driverKey
            : (string.IsNullOrWhiteSpace(classGuid) ? string.Empty : $"{classGuid}\\{driverKey}");
        if (string.IsNullOrWhiteSpace(relative)) return (string.Empty, string.Empty, string.Empty, string.Empty);
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Control\Class\{relative}");
            if (key is null) return (string.Empty, string.Empty, string.Empty, string.Empty);
            return (
                key.GetValue("DriverVersion")?.ToString()?.Trim() ?? string.Empty,
                NormalizeDriverDate(key.GetValue("DriverDate")?.ToString()),
                key.GetValue("ProviderName")?.ToString()?.Trim() ?? string.Empty,
                key.GetValue("InfPath")?.ToString()?.Trim() ?? string.Empty);
        }
        catch { return (string.Empty, string.Empty, string.Empty, string.Empty); }
    }

    /// <summary>
    /// "6-21-2006" → "20060621000000.000000+000" (CIM, lo que ya formatea la UI);
    /// CIM intacto; lo demás tal cual (honesto).
    /// </summary>
    public static string NormalizeDriverDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var trimmed = raw.Trim();
        if (trimmed.Length >= 8 && trimmed[..8].All(char.IsDigit)) return trimmed; // ya CIM
        if (DateTime.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date)
            || DateTime.TryParse(trimmed, System.Globalization.CultureInfo.CurrentCulture,
                System.Globalization.DateTimeStyles.None, out date))
            return date.ToString("yyyyMMdd000000.000000+000");
        return trimmed;
    }
}
