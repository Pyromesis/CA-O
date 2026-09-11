using CAO.Shared.Security;
using Xunit;

namespace CAO.Security.Tests;

/// <summary>
/// Fase red/latencia/storage: defrag solo-volumen, netsh Wi-Fi con nombre
/// validado y bcdedit de tick dinámico. Formas fijas o null.
/// </summary>
public sealed class SystemPolicyTests
{
    private static string System32(string tool) =>
        Path.Combine(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32"), tool);

    [Theory]
    [InlineData(SystemCommandKey.DefragAnalyze, new[] { "C:", "/A" })]
    [InlineData(SystemCommandKey.DefragAnalyze, new[] { "D:", "/A" })]
    [InlineData(SystemCommandKey.DefragHdd, new[] { "C:", "/D" })]
    [InlineData(SystemCommandKey.DefragHdd, new[] { "E:", "/D" })]
    public void DefragKeysResolveToCanonicalExecutable(SystemCommandKey key, string[] arguments)
    {
        Assert.Equal(System32("defrag.exe"), CommandPolicy.Resolve(key, arguments));
    }

    [Theory]
    [InlineData(SystemCommandKey.DefragAnalyze, new[] { "C:", "/D" })]
    [InlineData(SystemCommandKey.DefragHdd, new[] { "C:", "/A" })]
    [InlineData(SystemCommandKey.DefragAnalyze, new[] { "c:", "/A" })]
    [InlineData(SystemCommandKey.DefragAnalyze, new[] { "C", "/A" })]
    [InlineData(SystemCommandKey.DefragAnalyze, new[] { "C:\\", "/A" })]
    [InlineData(SystemCommandKey.DefragAnalyze, new[] { "C:", "/A", "extra" })]
    [InlineData(SystemCommandKey.DefragHdd, new[] { "C:;calc", "/D" })]
    public void DefragDeviationResolvesToNull(SystemCommandKey key, string[] arguments)
    {
        Assert.Null(CommandPolicy.Resolve(key, arguments));
    }

    [Theory]
    [InlineData("C:", true)]
    [InlineData("Z:", true)]
    [InlineData("c:", false)]
    [InlineData("C", false)]
    [InlineData("C:\\", false)]
    [InlineData("", false)]
    [InlineData("C:;x", false)]
    public void DriveVolumeValidatorIsStrict(string volume, bool expected)
    {
        Assert.Equal(expected, CommandPolicy.IsValidDriveVolume(volume));
    }

    [Fact]
    public void WlanAutoconfigResolvesWithHealthyName()
    {
        Assert.Equal(
            System32("netsh.exe"),
            CommandPolicy.Resolve(SystemCommandKey.NetShWlanAutoconfig,
                new[] { "wlan", "set", "autoconfig", "enabled=no", "interface=\"Wi-Fi\"" }));
        Assert.Equal(
            System32("netsh.exe"),
            CommandPolicy.Resolve(SystemCommandKey.NetShWlanAutoconfig,
                new[] { "wlan", "set", "autoconfig", "enabled=yes", "interface=\"Wi-Fi 2\"" }));
    }

    [Theory]
    [InlineData("wlan", "set", "autoconfig", "enabled=maybe", "interface=\"Wi-Fi\"")]
    [InlineData("wlan", "set", "autoconfig", "enabled=no", "interface=Wi-Fi")]
    [InlineData("wlan", "set", "autoconfig", "enabled=no", "interface=\"Wi-Fi|calc\"")]
    [InlineData("wlan", "set", "autoconfig", "enabled=no", "interface=\"..\\x\"")]
    [InlineData("wlan", "set", "autoconfig", "enabled=no", "interface=\"\"")]
    [InlineData("wlan", "set", "autoconfig", "enabled=no")]
    public void WlanAutoconfigDeviationResolvesToNull(params string[] arguments)
    {
        Assert.Null(CommandPolicy.Resolve(SystemCommandKey.NetShWlanAutoconfig, arguments));
    }

    [Theory]
    [InlineData("interface=\"Wi-Fi\"", true)]
    [InlineData("interface=\"Wi-Fi 2\"", true)]
    [InlineData("interface=\"Red (Casa)\"", true)]
    [InlineData("interface=Wi-Fi", false)]
    [InlineData("interface=\"Wi-Fi", false)]
    [InlineData("interface=\"\"", false)]
    [InlineData("interface=\" Wi-Fi\"", false)]
    [InlineData("interface=\"Wi-Fi\\\\x\"", false)]
    [InlineData("", false)]
    public void NetshInterfaceArgValidatorIsStrict(string arg, bool expected)
    {
        Assert.Equal(expected, CommandPolicy.IsValidNetshInterfaceArg(arg));
    }

    [Theory]
    [InlineData(SystemCommandKey.BcdEditDynamicTickYes, new[] { "/set", "{current}", "disabledynamictick", "yes" })]
    [InlineData(SystemCommandKey.BcdEditDynamicTickNo, new[] { "/set", "{current}", "disabledynamictick", "no" })]
    [InlineData(SystemCommandKey.BcdEditDynamicTickDelete, new[] { "/deletevalue", "{current}", "disabledynamictick" })]
    public void BcdTickKeysResolveToCanonicalExecutable(SystemCommandKey key, string[] arguments)
    {
        Assert.Equal(System32("bcdedit.exe"), CommandPolicy.Resolve(key, arguments));
    }

    [Theory]
    [InlineData(SystemCommandKey.BcdEditDynamicTickYes, new[] { "/set", "{current}", "disabledynamictick", "no" })]
    [InlineData(SystemCommandKey.BcdEditDynamicTickNo, new[] { "/deletevalue", "{current}", "disabledynamictick" })]
    [InlineData(SystemCommandKey.BcdEditDynamicTickDelete, new[] { "/deletevalue", "{current}", "useplatformclock" })]
    [InlineData(SystemCommandKey.BcdEditDynamicTickYes, new[] { "/set", "{current}", "useplatformclock", "yes" })]
    public void BcdTickDeviationResolvesToNull(SystemCommandKey key, string[] arguments)
    {
        Assert.Null(CommandPolicy.Resolve(key, arguments));
    }
}
