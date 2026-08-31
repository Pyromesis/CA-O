using System.Text.RegularExpressions;

namespace CAO.Infrastructure.PowerShell;

/// <summary>
/// DEPRECATED — Single authority now is <see cref="CAO.Shared.Security.CommandPolicy"/>.
/// Kept for backwards compat, but privileged execution uses CommandPolicy via SystemCommandGateway.
/// </summary>
[Obsolete("Usar CAO.Shared.Security.CommandPolicy como única autoridad. Será eliminado en v3.")]
public static partial class ElevatedCommandCatalog
{
    [GeneratedRegex("^(powercfg|netsh|bcdedit|fsutil|defrag)\\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AllowedExecutable();

    public static bool IsAllowed(string fileName, string arguments)
    {
        if (!AllowedExecutable().IsMatch(fileName))
            return false;
        var args = arguments.Trim();
        return fileName.StartsWith("powercfg", StringComparison.OrdinalIgnoreCase)
            ? IsAllowedPowerCfg(args)
            : fileName.StartsWith("netsh", StringComparison.OrdinalIgnoreCase)
                ? IsAllowedNetSh(args)
                : false;
    }

    private static bool IsAllowedPowerCfg(string args) =>
        Regex.IsMatch(args, "^/?(getactivescheme|/q|/query|/a query|/list|/l)$", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(args, @"^/?(s|setactive)\s+SCHEME_(MIN|MAX|BALANCED)$", RegexOptions.IgnoreCase);

    private static bool IsAllowedNetSh(string args) =>
        Regex.IsMatch(args, @"^advfirewall\s+(show\s+allprofiles(\s+state)?|set\s+allprofiles\s+state\s+(on|off))$",
            RegexOptions.IgnoreCase);
}
