using CAO.Shared.Security;
using Xunit;

namespace CAO.Security.Tests;

/// <summary>
/// CommandPolicy allowlist contract (FASE 4 / spec 93): every documented key
/// resolves to a canonical %SystemRoot% absolute executable only for an exact,
/// fixed argument shape; any deviation, unknown key or metacharacter must
/// resolve to null BEFORE any process is created.
/// </summary>
public sealed class CommandPolicyTests
{
    private static string System32(string tool) =>
        Path.Combine(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32"), tool);

    [Theory]
    [InlineData(SystemCommandKey.PowerCfgQueryActiveScheme, new[] { "/getactivescheme" }, "powercfg.exe")]
    [InlineData(SystemCommandKey.PowerCfgQueryAvailable, new[] { "/a" }, "powercfg.exe")]
    [InlineData(SystemCommandKey.PowerCfgSetActiveScheme, new[] { "/setactive", "SCHEME_MIN" }, "powercfg.exe")]
    [InlineData(SystemCommandKey.PowerCfgSetActiveScheme, new[] { "/setactive", "{8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c}" }, "powercfg.exe")]
    [InlineData(SystemCommandKey.PowerCfgDuplicateScheme, new[] { "/duplicatescheme", "{e9a42b02-d5df-448d-aa00-03f14749eb61}" }, "powercfg.exe")]
    [InlineData(SystemCommandKey.PowerCfgHibernateOff, new[] { "/h", "off" }, "powercfg.exe")]
    [InlineData(SystemCommandKey.PowerCfgHibernateOn, new[] { "/h", "on" }, "powercfg.exe")]
    [InlineData(SystemCommandKey.BcdEditEnumCurrent, new[] { "/enum", "{current}" }, "bcdedit.exe")]
    [InlineData(SystemCommandKey.BcdEditHypervisorOff, new[] { "/set", "{current}", "hypervisorlaunchtype", "off" }, "bcdedit.exe")]
    [InlineData(SystemCommandKey.BcdEditHypervisorRestore, new[] { "/set", "{current}", "hypervisorlaunchtype", "Auto" }, "bcdedit.exe")]
    [InlineData(SystemCommandKey.NetShTcpShowGlobal, new[] { "int", "tcp", "show", "global" }, "netsh.exe")]
    [InlineData(SystemCommandKey.NetShTcpAutotuningNormal, new[] { "int", "tcp", "set", "global", "autotuninglevel=normal" }, "netsh.exe")]
    [InlineData(SystemCommandKey.DefragC, new[] { "C:", "/O" }, "defrag.exe")]
    [InlineData(SystemCommandKey.WprStartCpuFileMode, new[] { "-start", "CPU", "-filemode" }, "wpr.exe")]
    [InlineData(SystemCommandKey.WprStopToDefaultFile, new[] { "-stop", "x.etl", "-overwrite" }, "wpr.exe")]
    [InlineData(SystemCommandKey.LogmanDeleteSession, new[] { "delete", "CAO-DPC", "-ets" }, "logman.exe")]
    public void KnownKeysResolveToCanonicalExecutable(SystemCommandKey key, string[] arguments, string tool)
    {
        var resolved = CommandPolicy.Resolve(key, arguments);
        Assert.Equal(System32(tool), resolved);
    }

    [Theory]
    [InlineData(SystemCommandKey.DefragC, new[] { "C:", "/B" })]        // defrag /B (boot) not allowed here
    [InlineData(SystemCommandKey.DefragC, new[] { "D:", "/O" })]        // only C: is allowed
    [InlineData(SystemCommandKey.DefragC, new[] { "C:" })]              // wrong token count
    [InlineData(SystemCommandKey.PowerCfgSetActiveScheme, new[] { "/setactive", "SCHEME_CUSTOM" })] // unknown scheme
    [InlineData(SystemCommandKey.BcdEditHypervisorOff, new[] { "/set", "{current}", "hypervisorlaunchtype", "on" })] // restore path is pinned to Auto
    [InlineData(SystemCommandKey.NetShTcpAutotuningNormal, new[] { "int", "tcp", "set", "global", "autotuninglevel=huge" })] // undocumented level
    public void UnexpectedArgumentsResolveToNull(SystemCommandKey key, string[] arguments)
    {
        Assert.Null(CommandPolicy.Resolve(key, arguments));
    }

    [Theory]
    [InlineData(SystemCommandKey.PowerCfgQueryActiveScheme, new[] { "/getactivescheme;whoami" })]
    [InlineData(SystemCommandKey.NetShTcpShowGlobal, new[] { "int", "tcp", "show", "global", "&", "whoami" })]
    [InlineData(SystemCommandKey.DefragC, new[] { "C:\\x" })]
    [InlineData(SystemCommandKey.DefragC, new[] { "C:", "/O", "|", "cmd" })]
    [InlineData(SystemCommandKey.PowerCfgHibernateOff, new[] { "/h", "off\" --" })]
    public void InjectionTokensResolveToNull(SystemCommandKey key, string[] arguments)
    {
        Assert.Null(CommandPolicy.Resolve(key, arguments));
    }

    [Theory]
    [InlineData(SystemCommandKey.NetShTcpShowGlobal, new[] { "" })]
    [InlineData(SystemCommandKey.DefragC, new[] { "C:", " " })]
    [InlineData(SystemCommandKey.PowerCfgQueryActiveScheme, new string[] { null! })]
    public void BlankOrEmptyTokensResolveToNull(SystemCommandKey key, string[] arguments)
    {
        Assert.Null(CommandPolicy.Resolve(key, arguments));
    }
}
