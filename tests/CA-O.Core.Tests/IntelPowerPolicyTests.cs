using CAO.Core.Overclock;
using CAO.Core.Overclock.Policy;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// The CPU policy is the security boundary for every powercfg call the
/// Overclock section makes. These tests need no hardware and no elevation:
/// that is the whole point of keeping the decision pure.
/// </summary>
public sealed class IntelPowerPolicyTests
{
    // powercfg output is localized; the parser must not depend on the label.
    private const string EnglishOutput = """
        GUID de subgroup de processor (54533251-82be-4824-96c1-47b60b740d00)  GUID de configuración (be337241-0d82-4146-a960-4f3749d470c3)
          Processor performance boost mode
          Power Setting GUID: be337241-0d82-4146-a960-4f3749d470c3
          Possible Setting Index: 000
          Possible Setting Friendly Name: Disabled
          Possible Setting Index: 001
          Possible Setting Friendly Name: Enabled
          Current AC Power Setting Index: 0x00000002
          Current DC Power Setting Index: 0x00000000
        """;

    private const string SpanishOutput = """
        GUID de subgrupo de processor (54533251-82be-4824-96c1-47b60b740d00)  GUID de configuración (be337241-0d82-4146-a960-4f3749d470c3)
          Modo de mejora del rendimiento del procesador
          Valor de configuración de energía actual de CA: 0x000000ff
          Valor de configuración de energía de CC: 0x00000064
        """;

    [Fact]
    public void TryResolve_MapsEveryCatalogKnobToAGuidPair()
    {
        Assert.True(IntelPowerPolicy.TryResolve(IntelPowerPolicy.BoostModeKnob, out PowerSettingTarget boost));
        Assert.Equal(IntelPowerPolicy.BoostModeSettingGuid, boost.Setting);
        Assert.Equal(IntelPowerPolicy.ProcessorSubgroupGuid, boost.Subgroup);

        Assert.True(IntelPowerPolicy.TryResolve(IntelPowerPolicy.MaxFrequencyKnob, out PowerSettingTarget freq));
        Assert.Equal(IntelPowerPolicy.MaxProcessorStateSettingGuid, freq.Setting);
    }

    [Theory]
    [InlineData("nvidia.gpu.powerLimit")]
    [InlineData("memory.smbios.nominalSpeed")]
    [InlineData("")]
    public void TryResolve_RefusesKnobsOutsideCpuPolicy(string knobId)
    {
        Assert.False(IntelPowerPolicy.TryResolve(new KnobId(knobId), out _));
    }

    [Fact]
    public void IsAllowedTarget_AcceptsTheTwoProcessorPairs_InAnyCase()
    {
        Assert.True(IntelPowerPolicy.IsAllowedTarget(
            IntelPowerPolicy.ProcessorSubgroupGuid.ToUpperInvariant(),
            IntelPowerPolicy.BoostModeSettingGuid.ToUpperInvariant()));
        Assert.True(IntelPowerPolicy.IsAllowedTarget(
            " 54533251-82be-4824-96c1-47b60b740d00 ",
            IntelPowerPolicy.MaxProcessorStateSettingGuid));
    }

    [Theory]
    [InlineData("2a737441-1930-4402-8d77-b2bebba308a3", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226")]
    [InlineData("54533251-82be-4824-96c1-47b60b740d00", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226")]
    [InlineData("501a4d13-42af-4429-9fd1-a8218c268e1d", "ee12f906-d277-4bcf-ad6c-e5a569d7c83d")]
    [InlineData("", "")]
    public void IsAllowedTarget_RefusesEveryOtherPair(string subgroup, string setting)
    {
        Assert.False(IntelPowerPolicy.IsAllowedTarget(subgroup, setting));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TryNormalize_AcceptsEveryBoostMode(int value)
    {
        Assert.True(IntelPowerPolicy.TryNormalize(BoostTarget(), value, out int normalized));
        Assert.Equal(value, normalized);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void TryNormalize_RefusesBoostModeOutsideTheEnum(int value)
    {
        Assert.False(IntelPowerPolicy.TryNormalize(BoostTarget(), value, out _));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(55)]
    [InlineData(100)]
    public void TryNormalize_AcceptsTheProcessorStateCeiling(int value)
    {
        Assert.True(IntelPowerPolicy.TryNormalize(FrequencyTarget(), value, out int normalized));
        Assert.Equal(value, normalized);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    [InlineData(101)]
    public void TryNormalize_RefusesCeilingOutsideTheProductRange(int value)
    {
        Assert.False(IntelPowerPolicy.TryNormalize(FrequencyTarget(), value, out _));
    }

    [Fact]
    public void BuildSetArguments_ProducesTheExactFiveArgumentCommand()
    {
        IReadOnlyList<string>? args = IntelPowerPolicy.BuildSetArguments(FrequencyTarget(), 75);

        Assert.NotNull(args);
        Assert.Equal(5, args.Count);
        Assert.Equal("/setacvalueindex", args[0]);
        Assert.Equal(IntelPowerPolicy.TargetScheme, args[1]);
        Assert.Equal(IntelPowerPolicy.ProcessorSubgroupGuid, args[2]);
        Assert.Equal(IntelPowerPolicy.MaxProcessorStateSettingGuid, args[3]);
        Assert.Equal("75", args[4]);
    }

    [Fact]
    public void BuildSetArguments_RefusesToBuildAnInvalidCommand()
    {
        Assert.Null(IntelPowerPolicy.BuildSetArguments(FrequencyTarget(), 500));
        Assert.Null(IntelPowerPolicy.BuildSetArguments(
            new PowerSettingTarget(IntelPowerPolicy.ProcessorSubgroupGuid, "48e6b7a6-50f5-4782-a5d4-53bb8f07e226"), 0));
    }

    [Fact]
    public void BuildQueryArguments_ProducesTheReadOnlyCommand()
    {
        IReadOnlyList<string>? args = IntelPowerPolicy.BuildQueryArguments(BoostTarget());

        Assert.NotNull(args);
        Assert.Equal(["/q", "SCHEME_CURRENT",
            IntelPowerPolicy.ProcessorSubgroupGuid, IntelPowerPolicy.BoostModeSettingGuid], args);
    }

    [Fact]
    public void ParseAcSettingIndex_ReadsTheFirstHexLiteral_InEnglishAndSpanish()
    {
        Assert.Equal(2, IntelPowerPolicy.ParseAcSettingIndex(EnglishOutput));
        Assert.Equal(255, IntelPowerPolicy.ParseAcSettingIndex(SpanishOutput));
    }

    [Fact]
    public void ParseDcSettingIndex_ReadsTheSecondHexLiteral()
    {
        Assert.Equal(0, IntelPowerPolicy.ParseDcSettingIndex(EnglishOutput));
        Assert.Equal(100, IntelPowerPolicy.ParseDcSettingIndex(SpanishOutput));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no hexadecimal literal here")]
    public void ParseAcSettingIndex_ReturnsNull_WhenItCannotInspect(string? stdout)
    {
        // Null means "could not inspect", never "the value is zero".
        Assert.Null(IntelPowerPolicy.ParseAcSettingIndex(stdout));
    }

    [Fact]
    public void ParseDcSettingIndex_ReturnsNull_WhenOnlyOneIndexIsPresent()
    {
        Assert.Null(IntelPowerPolicy.ParseDcSettingIndex("Current AC Power Setting Index: 0x00000002"));
    }

    [Fact]
    public void SettingValues_StageAndThenActivate_WhichIsWhyActivationIsMandatory()
    {
        // If this ever becomes false the backend's post-write activation step
        // silently stops being needed and the verify step would be reading a
        // stale value. Asserted so the invariant is documented in code.
        Assert.True(IntelPowerPolicy.RequiresSchemeActivation);
    }

    [Theory]
    [InlineData(CpuBoostMode.Disabled, 0)]
    [InlineData(CpuBoostMode.Enabled, 1)]
    [InlineData(CpuBoostMode.Aggressive, 2)]
    public void CpuBoostMode_MapsOneToOneOntoTheFirmwareEnum(CpuBoostMode mode, int expected)
    {
        Assert.Equal(expected, (int)mode);
    }

    private static PowerSettingTarget BoostTarget() =>
        new(IntelPowerPolicy.ProcessorSubgroupGuid, IntelPowerPolicy.BoostModeSettingGuid);

    private static PowerSettingTarget FrequencyTarget() =>
        new(IntelPowerPolicy.ProcessorSubgroupGuid, IntelPowerPolicy.MaxProcessorStateSettingGuid);
}

/// <summary>
/// The XTU verdict is the difference between "CA-O can tune your CPU" and
/// "CA-O cannot, and here is exactly why". Every branch must name a real,
/// localized cause.
/// </summary>
public sealed class XtuAvailabilityPolicyTests
{
    [Fact]
    public void Decide_OnAmd_OffersPolicyOnlyAndSaysWhyXtuIsIrrelevant()
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(Facts(vendor: "AuthenticAMD"));

        Assert.Equal(CpuBackendKind.PolicyOnly, verdict.Kind);
        Assert.False(verdict.OverclockingAvailable);
        Assert.Equal(XtuAvailabilityPolicy.ReasonAmdCpu, verdict.ReasonKey);
    }

    [Fact]
    public void Decide_OnAnUnidentifiedCpu_DoesNotGuess()
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(Facts(vendor: string.Empty));

        Assert.Equal(CpuBackendKind.PolicyOnly, verdict.Kind);
        Assert.Equal(XtuAvailabilityPolicy.ReasonUnknownCpu, verdict.ReasonKey);
    }

    [Fact]
    public void Decide_WithoutXtu_PointsAtTheInstallHint()
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(
            Facts(vendor: "GenuineIntel", installed: false));

        Assert.Equal(CpuBackendKind.PolicyOnly, verdict.Kind);
        Assert.Equal(XtuAvailabilityPolicy.ReasonXtuNotInstalled, verdict.ReasonKey);
        Assert.Equal(XtuAvailabilityPolicy.HintInstallXtu, verdict.RemedyHintKey);
    }

    [Fact]
    public void Decide_WithXtuInstalledButNoService_ReportsTheMissingService()
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(
            Facts(vendor: "GenuineIntel", installed: true, service: false));

        Assert.Equal(XtuAvailabilityPolicy.ReasonXtuServiceMissing, verdict.ReasonKey);
    }

    [Fact]
    public void Decide_WhenXtuCannotControlTheCpu_DoesNotClaimItCan()
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(
            Facts(vendor: "GenuineIntel", installed: true, service: true, ocSupported: false));

        Assert.False(verdict.OverclockingAvailable);
        Assert.Equal(XtuAvailabilityPolicy.ReasonXtuOcUnsupported, verdict.ReasonKey);
    }

    [Fact]
    public void Decide_OnALaptop_DoesNotWalkTheUserIntoALockedRatio()
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(
            Facts(vendor: "GenuineIntel", installed: true, service: true, ocSupported: true, laptop: true));

        Assert.False(verdict.OverclockingAvailable);
        Assert.Equal(XtuAvailabilityPolicy.ReasonXtuLaptopLocked, verdict.ReasonKey);
    }

    [Fact]
    public void Decide_WithEverythingPresent_EnablesTheGuidedFlow()
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(
            Facts(vendor: "GenuineIntel", installed: true, service: true, ocSupported: true, version: "16.1.1.0"));

        Assert.Equal(CpuBackendKind.XtuGuided, verdict.Kind);
        Assert.True(verdict.OverclockingAvailable);
        Assert.Equal(XtuAvailabilityPolicy.ReasonXtuAvailable, verdict.ReasonKey);
        Assert.Equal(XtuAvailabilityPolicy.HintOpenXtu, verdict.RemedyHintKey);
        Assert.Equal("16.1.1.0", verdict.DetectedVersion);
    }

    [Fact]
    public void Decide_WithoutAVersion_StillReportsGuided_WithAnExplicitUnknown()
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(
            Facts(vendor: "GenuineIntel", installed: true, service: true, ocSupported: true, version: null));

        Assert.Equal(CpuBackendKind.XtuGuided, verdict.Kind);
        Assert.Equal("unknown", verdict.DetectedVersion);
    }

    [Fact]
    public void Decide_NeverPromisesASignedMsrDriver()
    {
        // MsrSigned does not exist yet. Claiming it would send the user
        // looking for a control that cannot be there.
        XtuAvailability[] verdicts = Enumerable.Range(0, 2)
            .SelectMany(i => Enumerable.Range(0, 2).SelectMany(j => Enumerable.Range(0, 2)
                .SelectMany(k => new[]
                {
                    XtuAvailabilityPolicy.Decide(Facts("GenuineIntel", i == 1, j == 1, k == 1)),
                    XtuAvailabilityPolicy.Decide(Facts("AuthenticAMD", i == 1, j == 1, k == 1)),
                })))
            .ToArray();

        Assert.NotEmpty(verdicts);
        Assert.DoesNotContain(verdicts, v => v.Kind == CpuBackendKind.MsrSigned);
        Assert.DoesNotContain(verdicts, v => v.Kind == CpuBackendKind.None);
    }

    [Fact]
    public void Decide_RejectsNullFacts()
    {
        Assert.Throws<ArgumentNullException>(() => XtuAvailabilityPolicy.Decide(null!));
    }

    private static XtuProbeFacts Facts(
        string vendor,
        bool installed = false,
        bool service = false,
        bool ocSupported = false,
        string? version = null,
        bool laptop = false) =>
        new(vendor, laptop, installed, service, version, ocSupported);
}
