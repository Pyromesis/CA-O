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
        Regex.IsMatch(args, @"^/?(s|setactive)\s+SCHEME_(MIN|MAX|BALANCED)$", RegexOptions.IgnoreCase) ||
        ProcessorSettingPattern().IsMatch(args);

    /// <summary>
    /// Segunda whitelist, independiente de <c>CommandPolicy</c>: aunque la
    /// primera acepte el par, esta lo vuelve a comprobar. Solo el subgrupo
    /// processor y sus dos ajustes de política de CPU, contra SCHEME_CURRENT
    /// y contra nada más. El valor es un entero acotado por el regex, así que
    /// no hay forma de inyectar un argumento extra.
    /// </summary>
    [GeneratedRegex(
        @"^/?setacvalueindex\s+SCHEME_CURRENT\s+54533251-82be-4824-96c1-47b60b740d00\s+(be337241-0d82-4146-a960-4f3749d470c3|bc5038f7-23e0-4960-96da-33abaf5935ec)\s+\d{1,3}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProcessorSettingPattern();

    private static bool IsAllowedNetSh(string args) =>
        Regex.IsMatch(args, @"^advfirewall\s+(show\s+allprofiles(\s+state)?|set\s+allprofiles\s+state\s+(on|off))$",
            RegexOptions.IgnoreCase);
}
