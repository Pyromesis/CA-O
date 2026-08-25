using System.Text.RegularExpressions;

namespace CAO.Infrastructure.PowerShell;

/// <summary>
/// Static catalog of every elevated external command CA-O may run (spec 93).
/// Commands are complete constant strings; no user input is ever
/// interpolated. The privileged host refuses anything not in this catalog.
/// </summary>
public static partial class ElevatedCommandCatalog
{
    [GeneratedRegex("^(powercfg|netsh|bcdedit|fsutil|defrag)\\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AllowedExecutable();

    /// <summary>
    /// Validates that a (file, arguments) pair is an exact, known-safe call.
/// Argument shapes are constrained per tool; free-form text is rejected.
    /// </summary>
    public static bool IsAllowed(string fileName, string arguments)
    {
        if (!AllowedExecutable().IsMatch(fileName))
        {
            return false;
        }

        var args = arguments.Trim();
        return fileName.StartsWith("powercfg", StringComparison.OrdinalIgnoreCase)
            ? IsAllowedPowerCfg(args)
            : fileName.StartsWith("netsh", StringComparison.OrdinalIgnoreCase)
                ? IsAllowedNetSh(args)
                : false;
    }

    private static bool IsAllowedPowerCfg(string args) =>
        // Read queries and the two documented power scheme switches only.
        Regex.IsMatch(args, "^/?(getactivescheme|/q|/query|/a query|/list|/l)$", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(args, @"^/?(s|setactive)\s+SCHEME_(MIN|MAX|BALANCED)$", RegexOptions.IgnoreCase);

    private static bool IsAllowedNetSh(string args) =>
        Regex.IsMatch(args, @"^advfirewall\s+(show\s+allprofiles(\s+state)?|set\s+allprofiles\s+state\s+(on|off))$",
            RegexOptions.IgnoreCase);
}
