using CAO.Infrastructure.PowerShell;
using Xunit;

namespace CAO.Security.Tests;

/// <summary>
/// PowerShell/native command contract (spec 93): only complete, static,
/// allow-listed commands are accepted; interpolation or chained commands are
/// impossible to express through the catalog.
/// </summary>
public sealed class ElevatedCommandCatalogTests
{
    [Theory]
    [InlineData("powercfg.exe", "/getactivescheme")]
    [InlineData("powercfg.exe", "/q")]
    [InlineData("powercfg.exe", "/list")]
    [InlineData("netsh.exe", "advfirewall show allprofiles state")]
    public void AcceptsKnownStaticCommands(string file, string args)
    {
#pragma warning disable CS0618
        Assert.True(ElevatedCommandCatalog.IsAllowed(file, args));
#pragma warning restore CS0618
    }

    [Theory]
    [InlineData("powershell.exe", "-Command \"Remove-Item C:\\\"")]   // no PowerShell at all
    [InlineData("cmd.exe", "/c del C:\\Windows")]                     // no cmd
    [InlineData("powercfg.exe", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 100")] // not in catalog
    [InlineData("bcdedit.exe", "/set {current} hypervisorlaunchtype off")] // boot-level: never via generic runner
    public void RejectsNonCatalogExecutablesOrArguments(string file, string args)
    {
#pragma warning disable CS0618
        Assert.False(ElevatedCommandCatalog.IsAllowed(file, args));
#pragma warning restore CS0618
    }

    [Theory]
    [InlineData("powercfg.exe", "/q; whoami")]
    [InlineData("netsh.exe", "advfirewall show allprofiles & del /f C:\\x")]
    [InlineData("powercfg.exe", "$(calc.exe)")]
    public void RejectsInjectionAttempts(string file, string args)
    {
#pragma warning disable CS0618
        Assert.False(ElevatedCommandCatalog.IsAllowed(file, args));
#pragma warning restore CS0618
    }
}
