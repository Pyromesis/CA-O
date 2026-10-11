using CAO.Shared.Security;
using Xunit;

namespace CAO.Security.Tests;

/// <summary>
/// The Overclock CPU section writes two power-scheme values through the
/// privileged gateway. This is the allowlist that decides whether a given
/// argument vector is allowed to reach powercfg.exe at all, so the tests pin
/// both the pairs that are permitted and the ranges that are not.
/// </summary>
public sealed class CommandPolicyProcessorSettingTests
{
    private const string ProcessorSubgroup = "54533251-82be-4824-96c1-47b60b740d00";
    private const string BoostMode = "be337241-0d82-4146-a960-4f3749d470c3";
    private const string MaxProcessorState = "bc5038f7-23e0-4960-96da-33abaf5935ec";

    private static string System32(string tool) =>
        Path.Combine(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32"), tool);

    [Theory]
    [InlineData(BoostMode, "0")]
    [InlineData(BoostMode, "1")]
    [InlineData(BoostMode, "2")]
    [InlineData(MaxProcessorState, "20")]
    [InlineData(MaxProcessorState, "55")]
    [InlineData(MaxProcessorState, "100")]
    public void SetAcValueIndex_AllowsTheTwoProcessorSettings_WithTheirRealRanges(string setting, string value)
    {
        string? resolved = CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", "SCHEME_CURRENT", ProcessorSubgroup, setting, value]);

        Assert.Equal(System32("powercfg.exe"), resolved);
    }

    [Theory]
    [InlineData(BoostMode, "3")]
    [InlineData(BoostMode, "-1")]
    [InlineData(BoostMode, "")]
    [InlineData(MaxProcessorState, "19")]
    [InlineData(MaxProcessorState, "101")]
    [InlineData(MaxProcessorState, "500")]
    [InlineData(MaxProcessorState, "50;calc")]
    public void SetAcValueIndex_RejectsEveryValueOutsideTheRange(string setting, string value)
    {
        Assert.Null(CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", "SCHEME_CURRENT", ProcessorSubgroup, setting, value]));
    }

    [Theory]
    [InlineData("48e6b7a6-50f5-4782-a5d4-53bb8f07e226")] // USB selective suspend
    [InlineData("ee12f906-d277-4bcf-ad6c-e5a569d7c83d")] // PCIe link state
    [InlineData("deadbeef-0000-0000-0000-000000000000")]
    public void SetAcValueIndex_RejectsAnyOtherSettingInTheProcessorSubgroup(string setting)
    {
        Assert.Null(CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", "SCHEME_CURRENT", ProcessorSubgroup, setting, "1"]));
    }

    [Theory]
    [InlineData("501a4d13-42af-4429-9fd1-a8218c268e1d")] // PCIe link state subgroup
    [InlineData("2a737441-1930-4402-8d77-b2bebba308a3")] // USB subgroup
    public void SetAcValueIndex_RejectsTheProcessorSettingsInAnyOtherSubgroup(string subgroup)
    {
        Assert.Null(CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", "SCHEME_CURRENT", subgroup, MaxProcessorState, "100"]));
    }

    [Theory]
    [InlineData("SCHEME_BALANCED")]
    [InlineData("SCHEME_MIN")]
    [InlineData("SCHEME_GUIDED")]
    public void SetAcValueIndex_RefusesToWriteIntoAnySchemeButTheCurrentOne(string scheme)
    {
        Assert.Null(CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", scheme, ProcessorSubgroup, MaxProcessorState, "100"]));
    }

    [Theory]
    [InlineData(BoostMode)]
    [InlineData(MaxProcessorState)]
    public void QueryAcValueIndex_AllowsReadingBothProcessorSettings(string setting)
    {
        string? resolved = CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgQueryAcValueIndex,
            ["/q", "SCHEME_CURRENT", ProcessorSubgroup, setting]);

        Assert.Equal(System32("powercfg.exe"), resolved);
    }

    [Fact]
    public void QueryAcValueIndex_RejectsAnUnknownProcessorSetting()
    {
        Assert.Null(CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgQueryAcValueIndex,
            ["/q", "SCHEME_CURRENT", ProcessorSubgroup, "00000000-0000-0000-0000-000000000000"]));
    }

    [Fact]
    public void SetAcValueIndex_RejectsAnExtraTrailingArgument()
    {
        // The value whitelist is positional: appending an argument must break
        // the shape, not ride along as an ignored extra token.
        Assert.Null(CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", "SCHEME_CURRENT", ProcessorSubgroup, MaxProcessorState, "100", "extra"]));
    }

    [Fact]
    public void ExistingNonProcessorSettings_KeepTheirOriginalThreeValueRange()
    {
        string? resolved = CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", "SCHEME_CURRENT", "2a737441-1930-4402-8d77-b2bebba308a3",
                "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", "0"]);

        Assert.Equal(System32("powercfg.exe"), resolved);
        Assert.Null(CommandPolicy.Resolve(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", "SCHEME_CURRENT", "2a737441-1930-4402-8d77-b2bebba308a3",
                "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", "3"]));
    }
}
