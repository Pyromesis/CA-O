using CAO.Core.Overclock;
using CAO.Infrastructure.Overclock;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Exercises the NVIDIA WRITE path against a simulated NVML driver. No GPU is
/// touched.
///
/// The fake replaces only the driver, never the backend: every classification,
/// clamp, anchor lookup and read-back decision runs the production code, which
/// is the whole point of the <see cref="INvidiaTuningApi"/> seam.
///
/// The scenarios below are the ones Hypertune gets wrong or hides: a write that
/// does not stick, a driver that cannot do the write at all, a knob with no
/// public setter, and a batch restore that must not strand half the knobs.
/// </summary>
public sealed class NvidiaGpuWriteTests
{
    private const int Success = 0;
    private const int NotSupported = 3;
    private const int NoPermission = 4;
    private const int FunctionNotFound = 13;
    private const int UnknownFailure = 999;

    private static readonly KnobId PowerLimit = NvKnobs.PowerLimit;
    private static readonly KnobId ThermalLimit = NvKnobs.ThermalLimit;
    private static readonly KnobId CoreOffset = NvKnobs.CoreClockOffset;
    private static readonly KnobId MemoryOffset = NvKnobs.MemoryClockOffset;
    private static readonly KnobId FanTarget = NvKnobs.FanTarget;
    private static readonly KnobId ClockReadout = NvKnobs.ClockReadout;

    // --- The write that does not stick -------------------------------------

    [Fact]
    public async Task Write_Fails_WhenTheDriverAcceptsButTheValueDoesNotStick()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.CoreWriteSticks = false;
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(CoreOffset, 60_000_000d, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(NvidiaNvapiBackend.VerifyFailedReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task Write_NeverCallsADriverThatDidNotWrite_Blocked()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.CoreWriteSticks = false;
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(CoreOffset, 60_000_000d, CancellationToken.None);

        // A value that did not stick is a verification failure. Calling it
        // "the driver is too old" would send the user to update a driver that
        // was never the problem.
        Assert.False(result.DriverBlocked);
    }

    // --- The driver that cannot do the write --------------------------------

    [Theory]
    [InlineData(NotSupported)]
    [InlineData(FunctionNotFound)]
    public async Task Write_ReportsDriverBlocked_WhenTheDriverDoesNotImplementTheSetter(int status)
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.CoreWriteStatus = status;
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(CoreOffset, 60_000_000d, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.DriverBlocked);
    }

    [Fact]
    public async Task Write_ReportsNoPermission_WhenTheDriverRefusesForLackOfElevation()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.PowerWriteStatus = NoPermission;
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(PowerLimit, 110d, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.DriverBlocked);
        Assert.Equal(NvidiaNvapiBackend.NoPermissionReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task Write_ReportsRejection_WhenTheDriverAnswersACodeCaODoesNotClassify()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.MemoryWriteStatus = UnknownFailure;
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(MemoryOffset, 250_000_000d, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.DriverBlocked);
        Assert.Equal(NvidiaNvapiBackend.RejectedReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task Write_ReportsTheRealReason_WhenNvmlIsNotThereAtAll()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.IsAvailable = false;
        tuning.UnavailableReasonKey = "overclock-nvml-missing";
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(CoreOffset, 60_000_000d, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("overclock-nvml-missing", result.FailureReasonKey);
        Assert.Empty(tuning.Writes);
    }

    // --- Range discipline ---------------------------------------------------

    [Fact]
    public async Task Write_ClampsTheOffsetToWhatTheDriverAccepts()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.Device = tuning.Device! with { CoreOffsetMaxMegahertz = 300 };
        NvidiaNvapiBackend backend = new(tuning);

        // 900 MHz asked for, the driver allows 300.
        KnobWriteResult result = await backend.WriteAsync(CoreOffset, 900_000_000d, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("core=300", Assert.Single(tuning.Writes));
        Assert.Equal(300_000_000d, result.Applied!.Value.Scalar);
    }

    [Fact]
    public async Task Write_ClampsTheOffsetUpward_WhenALowerValueIsRequested()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.Device = tuning.Device! with { CoreOffsetMinMegahertz = -50 };
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(CoreOffset, -400_000_000d, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("core=-50", Assert.Single(tuning.Writes));
    }

    [Fact]
    public async Task Write_RefusesToInventAnOffsetRange_WhenTheDriverReportsNoBounds()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.Device = tuning.Device! with
        {
            CoreOffsetMinMegahertz = null,
            CoreOffsetMaxMegahertz = null,
        };
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(CoreOffset, 60_000_000d, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(NvidiaNvapiBackend.OffsetBoundsUnavailableReasonKey, result.FailureReasonKey);
        Assert.Empty(tuning.Writes);
    }

    [Fact]
    public async Task Write_RefusesAPowerLimit_WhenThereIsNoAnchorToMakePercentMeaningful()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.Device = tuning.Device! with
        {
            DefaultPowerLimitMilliwatts = null,
            MinPowerLimitMilliwatts = null,
        };
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(PowerLimit, 110d, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(NvidiaNvapiBackend.PowerAnchorUnavailableReasonKey, result.FailureReasonKey);
        Assert.Empty(tuning.Writes);
    }

    [Fact]
    public async Task Write_ReportsThePercentageActuallyApplied_NotTheOneRequested()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.Device = tuning.Device! with { MaxPowerLimitMilliwatts = 450_000 };
        NvidiaNvapiBackend backend = new(tuning);

        // 500% of the factory limit, clamped by the driver to 450 W = 150%.
        KnobWriteResult result = await backend.WriteAsync(PowerLimit, 500d, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("power=450000", Assert.Single(tuning.Writes));
        Assert.Equal(150d, result.Applied!.Value.Scalar);
    }

    [Fact]
    public async Task Write_FallsBackToTheHardwareMinimum_WhenTheFactoryLimitIsUnreadable()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.Device = tuning.Device! with { DefaultPowerLimitMilliwatts = null, MinPowerLimitMilliwatts = 200_000 };
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(PowerLimit, 150d, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("power=300000", Assert.Single(tuning.Writes));
    }

    // --- Knobs NVIDIA publishes no setter for --------------------------------

    [Theory]
    [InlineData("nvidia.gpu.thermalLimit")]
    [InlineData("nvidia.gpu.fanTarget")]
    [InlineData("nvidia.gpu.clockReadout")]
    public async Task Write_RefusesKnobsWithNoPublicVendorSetter(string knobId)
    {
        NvidiaNvapiBackend backend = new(FakeTuningApi.Valid());

        KnobWriteResult result = await backend.WriteAsync(knobId, 90d, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(NvidiaNvapiBackend.NoPublicSetterReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task CanWrite_IsTrue_OnlyForTheKnobsThatHaveAPublicSetter()
    {
        NvidiaNvapiBackend backend = new(FakeTuningApi.Valid());

        Assert.True(backend.CanWrite(PowerLimit));
        Assert.True(backend.CanWrite(CoreOffset));
        Assert.True(backend.CanWrite(MemoryOffset));

        Assert.False(backend.CanWrite(ThermalLimit));
        Assert.False(backend.CanWrite(FanTarget));
        Assert.False(backend.CanWrite(ClockReadout));
    }

    [Fact]
    public async Task CanWrite_IsFalse_WhenTheDeviceCannotBeDescribed()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.Device = null;
        NvidiaNvapiBackend backend = new(tuning);

        Assert.False(backend.CanWrite(CoreOffset));
        Assert.Equal("overclock-nvml-device-unavailable", backend.BlockReasonKey(CoreOffset));
    }

    [Fact]
    public async Task BlockReasonKey_ExplainsWhyAKnobIsInert_InsteadOfShowingADeadControl()
    {
        NvidiaNvapiBackend backend = new(FakeTuningApi.Valid());

        Assert.Null(backend.BlockReasonKey(CoreOffset));
        Assert.Equal(NvidiaNvapiBackend.NoPublicSetterReasonKey, backend.BlockReasonKey(FanTarget));
    }

    // --- Reading back in the catalog's unit ----------------------------------

    [Fact]
    public async Task Read_ReportsTheOffsetInHertz_SoTheCatalogUnitIsHonoured()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.CoreOffsetMegahertz = 120;
        NvidiaNvapiBackend backend = new(tuning);

        KnobRead read = await backend.ReadAsync(CoreOffset, CancellationToken.None);

        Assert.True(read.Success);
        Assert.Equal(120_000_000d, read.Value.Scalar);
    }

    [Fact]
    public async Task Read_ReportsThePowerLimitAsPercentOfTheFactoryLimit()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.PowerLimitMilliwatts = 330_000;
        NvidiaNvapiBackend backend = new(tuning);

        KnobRead read = await backend.ReadAsync(PowerLimit, CancellationToken.None);

        Assert.True(read.Success);
        Assert.Equal(110d, read.Value.Scalar);
    }

    [Fact]
    public async Task Read_FailsWithAReason_WhenTheOffsetIsUnreadable()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.CoreOffsetMegahertz = null;
        NvidiaNvapiBackend backend = new(tuning);

        KnobRead read = await backend.ReadAsync(CoreOffset, CancellationToken.None);

        Assert.False(read.Success);
        Assert.Equal("overclock-gpu-offset-unavailable", read.FailureReasonKey);
    }

    // --- Snapshot and restore ------------------------------------------------

    [Fact]
    public async Task Snapshot_StoresThePowerLimitInMilliwatts_NotInPercent()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.PowerLimitMilliwatts = 330_000;
        NvidiaNvapiBackend backend = new(tuning);

        UnitSnapshot snapshot = await backend.SnapshotAsync(CancellationToken.None);

        KeyValuePair<KnobId, KnobValue> stored = snapshot.Values.Single(p => p.Key == PowerLimit);
        Assert.Equal(330_000d, stored.Value.Scalar);
    }

    [Fact]
    public async Task Restore_DoesNotStrandTheOtherKnobs_WhenOneValueCannotBeWrittenBack()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.PowerWriteStatus = NoPermission;
        NvidiaNvapiBackend backend = new(tuning);

        UnitSnapshot snapshot = Snapshot(
            new KeyValuePair<KnobId, KnobValue>(PowerLimit, KnobValue.FromScalar(300_000d)),
            new KeyValuePair<KnobId, KnobValue>(CoreOffset, KnobValue.FromScalar(60_000_000d)));

        RestoreOutcome outcome = await backend.RestoreAsync(snapshot, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal(1, outcome.RestoredCount);
        Assert.False(outcome.Verified);
        Assert.Equal(NvidiaNvapiBackend.RestoreFailedReasonKey, outcome.FailureReasonKey);

        // The core offset still went back: one bad knob must not strand the rest
        // in a half-tuned state.
        Assert.Contains("core=60", tuning.Writes);
    }

    [Fact]
    public async Task Restore_Fails_WhenAWrittenBackValueDoesNotStick()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        tuning.CoreWriteSticks = false;
        NvidiaNvapiBackend backend = new(tuning);

        UnitSnapshot snapshot = Snapshot(
            new KeyValuePair<KnobId, KnobValue>(CoreOffset, KnobValue.FromScalar(60_000_000d)));

        RestoreOutcome outcome = await backend.RestoreAsync(snapshot, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal(0, outcome.RestoredCount);
    }

    [Fact]
    public async Task Restore_VerifiesOnlyWhenEveryValueWentBack()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        NvidiaNvapiBackend backend = new(tuning);

        UnitSnapshot snapshot = Snapshot(
            new KeyValuePair<KnobId, KnobValue>(PowerLimit, KnobValue.FromScalar(300_000d)),
            new KeyValuePair<KnobId, KnobValue>(CoreOffset, KnobValue.FromScalar(60_000_000d)),
            new KeyValuePair<KnobId, KnobValue>(MemoryOffset, KnobValue.FromScalar(250_000_000d)));

        RestoreOutcome outcome = await backend.RestoreAsync(snapshot, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.True(outcome.Verified);
        Assert.Equal(3, outcome.RestoredCount);
    }

    [Fact]
    public async Task Restore_OfAnEmptySnapshot_ChangesNothingAndClaimsSo()
    {
        NvidiaNvapiBackend backend = new(FakeTuningApi.Valid());

        UnitSnapshot snapshot = Snapshot();
        RestoreOutcome outcome = await backend.RestoreAsync(snapshot, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(0, outcome.RestoredCount);
    }

    // --- The honest ceiling --------------------------------------------------

    [Fact]
    public async Task Read_ShowsTheCeilingTheDriverEnforces_SoTheProfileCannotPromiseMore()
    {
        FakeTuningApi tuning = FakeTuningApi.Valid();
        NvidiaNvapiBackend backend = new(tuning);

        KnobWriteResult result = await backend.WriteAsync(CoreOffset, 60_000_000d, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(-500, tuning.Device!.CoreOffsetMinMegahertz);
        Assert.Equal(1500, tuning.Device.MemoryOffsetMaxMegahertz);
    }

    [Fact]
    public async Task Classify_UsesOnlyTheCodesNvmlPublishes()
    {
        Assert.Equal(NvidiaTuningWriteOutcome.Applied, NvmlInterop.ClassifyWrite(Success));
        Assert.Equal(NvidiaTuningWriteOutcome.DriverBlocked, NvmlInterop.ClassifyWrite(NotSupported));
        Assert.Equal(NvidiaTuningWriteOutcome.DriverBlocked, NvmlInterop.ClassifyWrite(FunctionNotFound));
        Assert.Equal(NvidiaTuningWriteOutcome.NoPermission, NvmlInterop.ClassifyWrite(NoPermission));
        Assert.Equal(NvidiaTuningWriteOutcome.Rejected, NvmlInterop.ClassifyWrite(UnknownFailure));
        Assert.Equal(NvidiaTuningWriteOutcome.Rejected, NvmlInterop.ClassifyWrite(2));
    }

    private static UnitSnapshot Snapshot(params KeyValuePair<KnobId, KnobValue>[] values) =>
        new(OverclockComponent.Gpu, values, DateTimeOffset.UnixEpoch, "580.68");
}

/// <summary>
/// A simulated NVIDIA driver. It records what was written and only changes its
/// reported state when the write is configured to stick, which is how a driver
/// that silently ignores a request is reproduced.
/// </summary>
internal sealed class FakeTuningApi : INvidiaTuningApi
{
    public bool IsAvailable { get; set; } = true;

    public string? UnavailableReasonKey { get; set; }

    public NvidiaTuningDevice? Device { get; set; }

    public int? PowerLimitMilliwatts { get; set; } = 300_000;

    public int? CoreOffsetMegahertz { get; set; }

    public int? MemoryOffsetMegahertz { get; set; }

    public int PowerWriteStatus { get; set; }

    public int CoreWriteStatus { get; set; }

    public int MemoryWriteStatus { get; set; }

    public bool PowerWriteSticks { get; set; } = true;

    public bool CoreWriteSticks { get; set; } = true;

    public bool MemoryWriteSticks { get; set; } = true;

    /// <summary>Every write the backend attempted, as <c>power=300000</c> style lines.</summary>
    public List<string> Writes { get; } = [];

    public static FakeTuningApi Valid() => new()
    {
        Device = new NvidiaTuningDevice(
            "580.68",
            MinPowerLimitMilliwatts: 100_000,
            MaxPowerLimitMilliwatts: 600_000,
            DefaultPowerLimitMilliwatts: 300_000,
            EnforcedPowerLimitMilliwatts: 320_000,
            CoreOffsetMinMegahertz: -500,
            CoreOffsetMaxMegahertz: 500,
            MemoryOffsetMinMegahertz: -500,
            MemoryOffsetMaxMegahertz: 1500),
        PowerLimitMilliwatts = 300_000,
    };

    public bool TryDescribe(out NvidiaTuningDevice? device)
    {
        device = Device;
        return device is not null;
    }

    public int? ReadPowerLimitMilliwatts() => PowerLimitMilliwatts;

    public int? ReadCoreOffsetMegahertz() => CoreOffsetMegahertz;

    public int? ReadMemoryOffsetMegahertz() => MemoryOffsetMegahertz;

    public int SetPowerLimitMilliwatts(int milliwatts)
    {
        Writes.Add(FormattableString.Invariant($"power={milliwatts}"));
        if (PowerWriteSticks && PowerWriteStatus == 0)
        {
            PowerLimitMilliwatts = milliwatts;
        }

        return PowerWriteStatus;
    }

    public int SetCoreOffsetMegahertz(int megahertz)
    {
        Writes.Add(FormattableString.Invariant($"core={megahertz}"));
        if (CoreWriteSticks && CoreWriteStatus == 0)
        {
            CoreOffsetMegahertz = megahertz;
        }

        return CoreWriteStatus;
    }

    public int SetMemoryOffsetMegahertz(int megahertz)
    {
        Writes.Add(FormattableString.Invariant($"memory={megahertz}"));
        if (MemoryWriteSticks && MemoryWriteStatus == 0)
        {
            MemoryOffsetMegahertz = megahertz;
        }

        return MemoryWriteStatus;
    }
}