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
    NetShInterfaceIpShowDns,
    NetShInterfaceIpSetDnsPrimary,
    NetShInterfaceIpSetDnsSecondary,
    NetShInterfaceIpSetDnsDhcp,
    IpConfigFlushDns,
    DefragC,
    WprStartCpuFileMode,
    WprStopToDefaultFile,
    LogmanDeleteSession,
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
        if (arguments.Any(arg => string.IsNullOrWhiteSpace(arg) || !SafeArg().IsMatch(arg)))
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
