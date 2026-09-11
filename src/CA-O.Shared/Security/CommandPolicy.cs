using System.Text.RegularExpressions;

namespace CAO.Shared.Security;

/// <summary>
/// Whitelisted system commands the privileged layer may execute (FASE 4).
/// Each key maps to ONE absolute-path executable and a strict argument
/// shape; nothing else can ever be spawned through the gateway.
/// </summary>
public enum SystemCommandKey
{
    PowerCfgQueryActiveScheme,
    PowerCfgQueryAvailable,
    PowerCfgSetActiveScheme,
    PowerCfgDuplicateScheme,
    PowerCfgHibernateOff,
    PowerCfgHibernateOn,
    BcdEditEnumCurrent,
    BcdEditHypervisorOff,
    BcdEditHypervisorRestore,
    NetShTcpShowGlobal,
    NetShTcpAutotuningNormal,
    NetShTcpCongestionDefault,
    NetShTcpCongestionCubic,
    NetShTcpLargeSendOffload,
    NetShTcpChecksumOffload,
    NetShUdpChecksumOffload,
    NetShWinsockReset,
    NetShIntIpReset,
    DismStartComponentCleanup,
    DismResetBase,
    FsutilDisableDeleteNotifyOff,
    FsutilDisableDeleteNotifyOn,
    FsutilQueryDeleteNotify,
    DefragRetrim,
    PowerCfgListSchemes,
    PowerCfgDeleteScheme,
    PowerCfgSetActiveCurrent,
    PowerCfgSetAcValueIndex,
    PowerCfgQueryAcValueIndex,
    SchTasksQuery,
    SchTasksDisable,
    SchTasksEnable,
    TaskKillDwm,
    TaskKillExplorer,
    W32tmResync,
    NetShInterfaceIpShowDns,
    NetShInterfaceIpSetDnsPrimary,
    NetShInterfaceIpSetDnsSecondary,
    NetShInterfaceIpSetDnsDhcp,
    IpConfigFlushDns,
    DefragC,
    WprStartCpuFileMode,
    WprStopToDefaultFile,
    LogmanDeleteSession,
    PnPUtilScanDevices,
    PnPUtilEnumProblemDevices,
    PnPUtilEnableDevice,
    PnPUtilRemoveDevice,
    PnPUtilAddDriver,
    PnPUtilEnumDevice,
    PnPUtilExportDriver,
    ExpandCab,
    DefragAnalyze,
    DefragHdd,
    NetShWlanAutoconfig,
    BcdEditDynamicTickYes,
    BcdEditDynamicTickNo,
    BcdEditDynamicTickDelete,
}

/// <summary>Normalized result captured by the gateway.</summary>
public sealed record PrivilegedCommandResult(
    int ExitCode,
    string StdOut,
    string StdErr,
    bool TimedOut)
{
    public bool Success => ExitCode == 0 && !TimedOut;
}

/// <summary>
/// Declarative policy for privileged execution (FASE 4). Paths are
/// canonical %SystemRoot% absolutes, so PATH hijacking cannot redirect them;
/// argument tokens are exact-match strings, so chaining/redirection/
/// injection are rejected before any process is created.
/// </summary>
public static partial class CommandPolicy
{
    [GeneratedRegex(@"^[a-zA-Z0-9_\-./\\:=\{\}\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeArg();

    /// <summary>
    /// Resolves an exact (key, arguments) pair to the canonical executable
    /// path. Returns null for ANY deviation: unknown keys, unexpected token
    /// counts, or metacharacters (&amp; | ; &lt; &gt; " ' % ^ newlines).
    /// </summary>
    public static string? Resolve(SystemCommandKey key, IReadOnlyList<string> arguments)
    {
        // Los Instance ID PNP contienen '&' legítimo (p. ej. HDAUDIO\FUNC_01&VEN_...),
        // vetado por SafeArg, y las rutas INF reales llevan espacios/paréntesis:
        // esas claves usan su propio validador estricto (sin shell de por medio:
        // ArgumentList nunca interpreta metacaracteres).
        var customValidatedKey = key is SystemCommandKey.PnPUtilEnableDevice
            or SystemCommandKey.PnPUtilRemoveDevice
            or SystemCommandKey.PnPUtilEnumDevice
            or SystemCommandKey.PnPUtilExportDriver
            or SystemCommandKey.ExpandCab
            or SystemCommandKey.NetShWlanAutoconfig
            or SystemCommandKey.PnPUtilAddDriver;
        if (!customValidatedKey && arguments.Any(arg => string.IsNullOrWhiteSpace(arg) || !SafeArg().IsMatch(arg)))
        {
            return null;
        }

        var system32 = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32");
        var powershell = Path.Combine(system32, @"WindowsPowerShell\v1.0\powershell.exe");

        return key switch
        {
            SystemCommandKey.PowerCfgQueryActiveScheme when Eq(arguments, "/getactivescheme") =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.PowerCfgQueryAvailable when Eq(arguments, "/a") =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.PowerCfgSetActiveScheme when arguments.Count == 2 &&
                arguments[0] == "/setactive" &&
                (arguments[1] is "SCHEME_MIN" or "SCHEME_MAX" or "SCHEME_BALANCED" ||
                 IsPowerSchemeGuid(arguments[1])) =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.PowerCfgDuplicateScheme when arguments.Count == 2 &&
                arguments[0] == "/duplicatescheme" && IsPowerSchemeGuid(arguments[1]) =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.PowerCfgHibernateOff when Eq(arguments, "/h", "off") =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.PowerCfgHibernateOn when Eq(arguments, "/h", "on") =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.BcdEditEnumCurrent when Eq(arguments, "/enum", "{current}") =>
                Path.Combine(system32, "bcdedit.exe"),

            SystemCommandKey.BcdEditHypervisorOff when Eq(arguments,
                "/set", "{current}", "hypervisorlaunchtype", "off") =>
                Path.Combine(system32, "bcdedit.exe"),

            // Restore is pinned to Auto: callers map a captured "Off" to Auto
            // upstream (captured Off meant hypervisor was already off).
            SystemCommandKey.BcdEditHypervisorRestore when Eq(arguments,
                "/set", "{current}", "hypervisorlaunchtype", "Auto") =>
                Path.Combine(system32, "bcdedit.exe"),

            SystemCommandKey.NetShTcpShowGlobal when Eq(arguments,
                "int", "tcp", "show", "global") =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShTcpAutotuningNormal when arguments.Count == 5 &&
                arguments[0] == "int" && arguments[1] == "tcp" &&
                arguments[2] == "set" && arguments[3] == "global" &&
                arguments[4].StartsWith("autotuninglevel=", StringComparison.Ordinal) &&
                AutotuningLevels.Contains(arguments[4]["autotuninglevel=".Length..]) =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShTcpCongestionDefault when arguments.Count == 5 &&
                arguments[0] == "int" && arguments[1] == "tcp" &&
                arguments[2] == "set" && arguments[3] == "global" &&
                arguments[4] == "congestionprovider=default" =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShTcpLargeSendOffload when arguments.Count == 5 &&
                arguments[0] == "int" && arguments[1] == "tcp" &&
                arguments[2] == "set" && arguments[3] == "global" &&
                (arguments[4] == "large=enabled" || arguments[4] == "large=disabled") =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShTcpChecksumOffload when arguments.Count == 5 &&
                arguments[0] == "int" && arguments[1] == "tcp" &&
                arguments[2] == "set" && arguments[3] == "global" &&
                (arguments[4] == "checksum=enabled" || arguments[4] == "checksum=disabled") =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShUdpChecksumOffload when arguments.Count == 5 &&
                arguments[0] == "int" && arguments[1] == "udp" &&
                arguments[2] == "set" && arguments[3] == "global" &&
                (arguments[4] == "checksum=enabled" || arguments[4] == "checksum=disabled") =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShTcpCongestionCubic when Eq(arguments,
                "int", "tcp", "set", "global", "congestionprovider=cubic") =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShWinsockReset when Eq(arguments,
                "winsock", "reset") =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShIntIpReset when Eq(arguments,
                "int", "ip", "reset") =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.DismStartComponentCleanup when Eq(arguments,
                "/Online", "/Cleanup-Image", "/StartComponentCleanup") =>
                Path.Combine(system32, "dism.exe"),

            SystemCommandKey.DismResetBase when Eq(arguments,
                "/Online", "/Cleanup-Image", "/StartComponentCleanup", "/ResetBase") =>
                Path.Combine(system32, "dism.exe"),

            SystemCommandKey.FsutilDisableDeleteNotifyOff when Eq(arguments,
                "behavior", "set", "DisableDeleteNotify", "0") =>
                Path.Combine(system32, "fsutil.exe"),

            SystemCommandKey.FsutilDisableDeleteNotifyOn when Eq(arguments,
                "behavior", "set", "DisableDeleteNotify", "1") =>
                Path.Combine(system32, "fsutil.exe"),

            SystemCommandKey.FsutilQueryDeleteNotify when Eq(arguments,
                "behavior", "query", "DisableDeleteNotify") =>
                Path.Combine(system32, "fsutil.exe"),

            SystemCommandKey.DefragRetrim when Eq(arguments, "C:", "/L") =>
                Path.Combine(system32, "defrag.exe"),

            SystemCommandKey.PowerCfgListSchemes when Eq(arguments, "/L") =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.PowerCfgDeleteScheme when arguments.Count == 2 &&
                arguments[0] == "/delete" && IsPowerSchemeGuid(arguments[1]) =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.PowerCfgSetActiveCurrent when Eq(arguments,
                "/setactive", "SCHEME_CURRENT") =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.PowerCfgSetAcValueIndex when arguments.Count == 5 &&
                arguments[0] == "/setacvalueindex" && arguments[1] == "SCHEME_CURRENT" &&
                IsAllowedPowerSetting(arguments[2], arguments[3]) &&
                (arguments[4] is "0" or "1" or "2") =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.PowerCfgQueryAcValueIndex when arguments.Count == 4 &&
                arguments[0] == "/q" && arguments[1] == "SCHEME_CURRENT" &&
                IsAllowedPowerSetting(arguments[2], arguments[3]) =>
                Path.Combine(system32, "powercfg.exe"),

            SystemCommandKey.SchTasksQuery when Eq(arguments,
                "/Query", "/FO", "CSV", "/V") =>
                Path.Combine(system32, "schtasks.exe"),

            SystemCommandKey.SchTasksDisable when arguments.Count == 4 &&
                arguments[0] == "/Change" && arguments[1] == "/TN" &&
                IsValidTaskName(arguments[2]) && arguments[3] == "/DISABLE" =>
                Path.Combine(system32, "schtasks.exe"),

            SystemCommandKey.SchTasksEnable when arguments.Count == 4 &&
                arguments[0] == "/Change" && arguments[1] == "/TN" &&
                IsValidTaskName(arguments[2]) && arguments[3] == "/ENABLE" =>
                Path.Combine(system32, "schtasks.exe"),

            SystemCommandKey.TaskKillDwm when Eq(arguments,
                "/F", "/IM", "dwm.exe") =>
                Path.Combine(system32, "taskkill.exe"),

            SystemCommandKey.TaskKillExplorer when Eq(arguments,
                "/F", "/IM", "explorer.exe") =>
                Path.Combine(system32, "taskkill.exe"),

            SystemCommandKey.W32tmResync when Eq(arguments, "/resync") =>
                Path.Combine(system32, "w32tm.exe"),

            SystemCommandKey.NetShInterfaceIpShowDns when Eq(arguments,
                "interface", "ip", "show", "dns") =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShInterfaceIpSetDnsPrimary when arguments.Count == 7 &&
                arguments[0] == "interface" && arguments[1] == "ip" &&
                arguments[2] == "set" && arguments[3] == "dns" &&
                IsValidInterfaceName(arguments[4]) && arguments[5] == "static" && IsValidIp(arguments[6]) =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShInterfaceIpSetDnsSecondary when arguments.Count == 6 &&
                arguments[0] == "interface" && arguments[1] == "ip" &&
                arguments[2] == "add" && arguments[3] == "dns" &&
                IsValidInterfaceName(arguments[4]) && IsValidIp(arguments[5]) =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.NetShInterfaceIpSetDnsDhcp when arguments.Count == 6 &&
                arguments[0] == "interface" && arguments[1] == "ip" &&
                arguments[2] == "set" && arguments[3] == "dns" &&
                IsValidInterfaceName(arguments[4]) && arguments[5] == "dhcp" =>
                Path.Combine(system32, "netsh.exe"),

            SystemCommandKey.IpConfigFlushDns when Eq(arguments, "/flushdns") =>
                Path.Combine(system32, "ipconfig.exe"),

            SystemCommandKey.DefragC when Eq(arguments, "C:", "/O") =>
                Path.Combine(system32, "defrag.exe"),

            // Desfragmentación honesta: analizar y desfragmentar SOLO el
            // volumen indicado (letra A-Z). El que decide HDD vs SSD es el
            // motor (DiskMediaDetector); aquí solo la forma es fija.
            SystemCommandKey.DefragAnalyze when arguments.Count == 2 &&
                IsValidDriveVolume(arguments[0]) && arguments[1] == "/A" =>
                Path.Combine(system32, "defrag.exe"),

            SystemCommandKey.DefragHdd when arguments.Count == 2 &&
                IsValidDriveVolume(arguments[0]) && arguments[1] == "/D" =>
                Path.Combine(system32, "defrag.exe"),

            // Wi-Fi: solo autoconfig on/off en una interfaz con nombre sano.
            // El nombre viaja como un único argumento interface="..." (sin
            // shell: ArgumentList nunca interpreta las comillas).
            SystemCommandKey.NetShWlanAutoconfig when arguments.Count == 5 &&
                arguments[0] == "wlan" && arguments[1] == "set" &&
                arguments[2] == "autoconfig" &&
                (arguments[3] == "enabled=no" || arguments[3] == "enabled=yes") &&
                IsValidNetshInterfaceArg(arguments[4]) =>
                Path.Combine(system32, "netsh.exe"),

            // Dynamic tick: valores documentados sí/no + borrado (default).
            SystemCommandKey.BcdEditDynamicTickYes when Eq(arguments,
                "/set", "{current}", "disabledynamictick", "yes") =>
                Path.Combine(system32, "bcdedit.exe"),

            SystemCommandKey.BcdEditDynamicTickNo when Eq(arguments,
                "/set", "{current}", "disabledynamictick", "no") =>
                Path.Combine(system32, "bcdedit.exe"),

            SystemCommandKey.BcdEditDynamicTickDelete when Eq(arguments,
                "/deletevalue", "{current}", "disabledynamictick") =>
                Path.Combine(system32, "bcdedit.exe"),

            // FASE 20: kernel trace lifecycle (DPC/ISR). Fixed profile and
            // fixed output location; cleanup is guaranteed by the collector.
            SystemCommandKey.WprStartCpuFileMode when Eq(arguments,
                "-start", "CPU", "-filemode") =>
                Path.Combine(system32, "wpr.exe"),

            SystemCommandKey.WprStopToDefaultFile when arguments.Count == 3 &&
                arguments[0] == "-stop" && arguments[2] == "-overwrite" =>
                Path.Combine(system32, "wpr.exe"),

            SystemCommandKey.LogmanDeleteSession when Eq(arguments,
                "delete", "CAO-DPC", "-ets") =>
                Path.Combine(system32, "logman.exe"),

            // Fase drivers: pnputil solo con formas fijas; el ID de instancia
            // va validado por IsValidPnpInstanceId (sin espacios, comillas ni
            // metacaracteres de shell) y viaja por ArgumentList, nunca por shell.
            SystemCommandKey.PnPUtilScanDevices when Eq(arguments, "/scan-devices") =>
                Path.Combine(system32, "pnputil.exe"),

            SystemCommandKey.PnPUtilEnumProblemDevices when Eq(arguments, "/enum-devices", "/problem") =>
                Path.Combine(system32, "pnputil.exe"),

            SystemCommandKey.PnPUtilEnableDevice when arguments.Count == 2 &&
                arguments[0] == "/enable-device" && IsValidPnpInstanceId(arguments[1]) =>
                Path.Combine(system32, "pnputil.exe"),

            SystemCommandKey.PnPUtilRemoveDevice when arguments.Count == 2 &&
                arguments[0] == "/remove-device" && IsValidPnpInstanceId(arguments[1]) =>
                Path.Combine(system32, "pnputil.exe"),

            SystemCommandKey.PnPUtilEnumDevice when arguments.Count == 3 &&
                arguments[0] == "/enum-devices" && arguments[1] == "/instanceid" && IsValidPnpInstanceId(arguments[2]) =>
                Path.Combine(system32, "pnputil.exe"),

            SystemCommandKey.PnPUtilExportDriver when arguments.Count == 3 &&
                arguments[0] == "/export-driver" && IsValidPnpInstanceId(arguments[1]) && IsExportDriverDest(arguments[2]) =>
                Path.Combine(system32, "pnputil.exe"),

            SystemCommandKey.ExpandCab when arguments.Count == 3 &&
                arguments[0].EndsWith(".cab", StringComparison.OrdinalIgnoreCase) && IsCatalogPackagePath(arguments[0]) &&
                arguments[1] == "-F:*" && IsCatalogPackagePath(arguments[2]) =>
                Path.Combine(system32, "expand.exe"),

            SystemCommandKey.PnPUtilAddDriver when arguments.Count == 3 &&
                arguments[0] == "/add-driver" && IsValidInfPath(arguments[1]) && arguments[2] == "/install" =>
                Path.Combine(system32, "pnputil.exe"),

            _ => null,
        };
    }

    // Only the five documented autotuning levels are restorable.
    private static readonly HashSet<string> AutotuningLevels = new(StringComparer.Ordinal)
    {
        "disabled", "highlyrestricted", "restricted", "normal", "experimental",
    };

    private static readonly Regex GuidShape = new(
        @"^\{?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}?$",
        RegexOptions.CultureInvariant);

    private static bool IsPowerSchemeGuid(string token) => GuidShape.IsMatch(token);

    /// <summary>
    /// Únicos pares (subgrupo, ajuste) de powercfg que CA-O puede tocar:
    /// ahorro USB, PCIe Link State y adaptador Wi-Fi. Cualquier otro GUID se rechaza.
    /// </summary>
    private static bool IsAllowedPowerSetting(string subgroup, string setting) =>
        (subgroup, setting) switch
        {
            // USB selective suspend: 0 = desactivado
            ("2a737441-1930-4402-8d77-b2bebba308a3", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226") => true,
            // PCIe Link State Power Management: 0 = desactivado
            ("501a4d13-42af-4429-9fd1-a8218c268e1d", "ee12f906-d277-4bcf-ad6c-e5a569d7c83d") => true,
            // Wireless adapter power saving: 0 = máximo rendimiento
            ("19cbb8fa-5279-450e-9fac-8a3d5fedd0c1", "12bbebe6-58d6-4636-95bb-3217ef867c1a") => true,
            _ => false,
        };

    private static bool IsValidTaskName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 256 &&
        name.StartsWith('\\') && !name.Contains("..") && SafeArg().IsMatch(name);

    [GeneratedRegex(@"^[A-Za-z0-9\\&_\-+#.(){}]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PnpInstanceId();

    /// <summary>Volumen fijo para defrag: una letra A-Z + dos puntos.</summary>
    public static bool IsValidDriveVolume(string volume) =>
        !string.IsNullOrWhiteSpace(volume) && volume.Length == 2 &&
        volume[0] is >= 'A' and <= 'Z' && volume[1] == ':';

    [GeneratedRegex(@"^[A-Za-z0-9 _\-()]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NetshInterfaceName();

    /// <summary>
    /// Nombre de interfaz netsh sano ("Wi-Fi", "Wi‑Fi 2"): sin metacaracteres
    /// de shell, sin barras (rutas), sin puntos suspensivos. El argumento
    /// completo es interface="nombre" y se valida entero.
    /// </summary>
    public static bool IsValidNetshInterfaceArg(string arg)
    {
        const string prefix = "interface=\"";
        if (string.IsNullOrWhiteSpace(arg) || arg.Length > 80) return false;
        if (!arg.StartsWith(prefix, StringComparison.Ordinal) || !arg.EndsWith('"')) return false;
        var name = arg[prefix.Length..^1];
        if (name.Length == 0 || name.Length > 64 || name.Contains("..")) return false;
        if (name[0] == ' ' || name[^1] == ' ') return false;
        return NetshInterfaceName().IsMatch(name);
    }

    /// <summary>
    /// Instance ID PNP estricto (p. ej. HDAUDIO\FUNC_01&amp;VEN_10EC&amp;DEV_0283...,
    /// HID\{00001812-...}_DEV_... de Bluetooth, STORAGE\VOLUME\{guid}...).
    /// Las llaves son legítimas en IDs reales; siguen vetados espacios,
    /// comillas, ';', '|', '%', '^', '$', '`', saltos y '..'. El token viaja
    /// por ArgumentList (sin shell).
    /// </summary>
    public static bool IsValidPnpInstanceId(string id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 256 && !id.Contains("..") && PnpInstanceId().IsMatch(id);

    /// <summary>
    /// UpdateId de Windows Update: GUID estricto. Nunca se ejecuta nada con
    /// él: solo se compara contra los IDs de una búsqueda fresca del propio
    /// servicio; lo que no venga de WU no se instala.
    /// </summary>
    public static bool IsValidWindowsUpdateId(string id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && Guid.TryParse(id, out _);

    /// <summary>Raíz canónica del respaldo de drivers (%ProgramData%\CA-O\DriverBackup).</summary>
    public static string ExportDriverBackupRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CA-O", "DriverBackup");

    /// <summary>
    /// Destino de /export-driver: SIEMPRE bajo la raíz canónica, un solo
    /// nivel, charset de archivo seguro, sin '..'. El servicio construye el
    /// destino; esto es defensa en profundidad.
    /// </summary>
    public static bool IsExportDriverDest(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260) return false;
        var root = ExportDriverBackupRoot() + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        var leaf = path[root.Length..];
        if (leaf.Length == 0 || leaf.Length > 80 || leaf.Contains("..")) return false;
        if (leaf.Any(c => c is '\\' or '/' or ':' or '"' or '\'' || char.IsControl(c))) return false;
        return leaf.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' || c == '.');
    }

    /// <summary>
    /// Hardware ID para buscar en el catálogo (p. ej. PCI\VEN_8086&amp;DEV_AE50...).
    /// Mismo charset que Instance ID pero sin espacios ni '..'. Solo viaja en
    /// la query URL (escaped), nunca a procesos.
    /// </summary>
    public static bool IsValidCatalogHardwareId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 256 && !id.Contains("..") && PnpInstanceId().IsMatch(id);

    /// <summary>Raíz canónica de descargas del catálogo (%ProgramData%\CA-O\DriverDownloads).</summary>
    public static string CatalogDriverDownloadRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CA-O", "DriverDownloads");

    /// <summary>
    /// Ruta de paquete del catálogo: bajo la raíz canónica (.cab descargado o
    /// carpeta extraída). El servicio construye ambas desde el UpdateId GUID.
    /// </summary>
    public static bool IsCatalogPackagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260) return false;
        var root = CatalogDriverDownloadRoot() + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        var rel = path[root.Length..];
        if (rel.Length == 0 || rel.Length > 120 || rel.Contains("..")) return false;
        return rel.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' || c is '.' || c == '\\');
    }

    private static readonly HashSet<string> CatalogDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "download.windowsupdate.com",
        "catalog.s.download.windowsupdate.com",
        "catalog.download.windowsupdate.com",
        "dl.delivery.mp.microsoft.com",
        "download.microsoft.com",
    };

    /// <summary>
    /// Solo HTTPS en hosts de descarga de Microsoft. La URL viene del
    /// catálogo, pero se valida igual: nada fuera de Microsoft se descarga.
    /// </summary>
    public static bool IsAllowedCatalogHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return false;
        return CatalogDownloadHosts.Contains(uri.Host);
    }

    /// <summary>
    /// Ruta INF estricta para pnputil /add-driver: absoluta, extensión .inf,
    /// sin '..', sin ADS (':' solo tras la unidad), sin comillas ni control.
    /// Se permiten espacios y paréntesis de carpetas reales de Descargas.
    /// </summary>
    public static bool IsValidInfPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260) return false;
        if (!Path.IsPathFullyQualified(path)) return false;
        if (!".inf".Equals(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase)) return false;
        if (path.Contains("..")) return false;
        if (path.IndexOf(':', 2) >= 0) return false;
        if (path.Any(c => c is '"' or '\'' or '\n' or '\r' or '\t' or '\0' || char.IsControl(c))) return false;
        return true;
    }

    private static bool IsValidInterfaceName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 64 && SafeArg().IsMatch(name);

    private static bool IsValidIp(string ip) =>
        System.Net.IPAddress.TryParse(ip, out var addr) && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

    private static bool Eq(IReadOnlyList<string> arguments, params ReadOnlySpan<string> expected)
    {
        if (arguments.Count != expected.Length)
        {
            return false;
        }

        for (var index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(arguments[index], expected[index], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }
}
