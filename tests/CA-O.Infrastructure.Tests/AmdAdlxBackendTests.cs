using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Infrastructure.Overclock;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

/// <summary>
/// Exercises the AMD backend against a fake tuning surface. The ADLX vtable
/// chain below was verified against the official GPUOpen headers
/// (SDK v2.0.0.125) but has never executed on AMD silicon: the build machine
/// carries an NVIDIA card. These tests prove the backend logic, not the
/// driver conversation.
/// </summary>
public sealed class AmdAdlxBackendTests
{
    private const int AdlxOk = 0;
    private const int AdlxAlreadyEnabled = 1;
    private const int AdlxAlreadyInitialized = 2;
    private const int AdlxNotSupported = 12;
    private const int AdlxGpuInUse = 15;
    private const int AdlxResetNeeded = 18;
    private const int AdlxUnknown = 999;

    [Theory]
    [InlineData(AdlxOk)]
    [InlineData(AdlxAlreadyEnabled)]
    [InlineData(AdlxAlreadyInitialized)]
    public async Task ClassifyWrite_TreatsSuccessFamily_AsApplied(int status)
    {
        Assert.Equal(AmdTuningWriteOutcome.Applied, AmdAdlxInterop.ClassifyWrite(status));
    }

    [Fact]
    public async Task ClassifyWrite_TreatsNotSupported_AsDriverBlocked()
    {
        Assert.Equal(AmdTuningWriteOutcome.DriverBlocked, AmdAdlxInterop.ClassifyWrite(AdlxNotSupported));
    }

    [Fact]
    public async Task ClassifyWrite_KeepsResetNeeded_AndGpuInUse_AsTheirOwnReasons()
    {
        Assert.Equal(AmdTuningWriteOutcome.ResetNeeded, AmdAdlxInterop.ClassifyWrite(AdlxResetNeeded));
        Assert.Equal(AmdTuningWriteOutcome.GpuInUse, AmdAdlxInterop.ClassifyWrite(AdlxGpuInUse));
    }

    [Fact]
    public async Task ClassifyWrite_TreatsAnythingElse_AsRejected_WithTheRawCode()
    {
        Assert.Equal(AmdTuningWriteOutcome.Rejected, AmdAdlxInterop.ClassifyWrite(AdlxUnknown));
    }

    [Fact]
    public async Task Write_ClampsToTheDriverRange_AndReportsWhatTheDriverGot()
    {
        var tuning = FakeAmdTuningApi.Valid();
        var backend = new AmdAdlxBackend(tuning);

        KnobWriteResult result = await backend.WriteAsync(AmdKnobs.PowerLimit, KnobValue.FromScalar(500d), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["power=120"], tuning.Writes);
        Assert.Equal(120d, result.Applied!.Value.Scalar);
    }

    [Fact]
    public async Task Write_ClampsUp_WhenTheRequestIsBelowTheMinimum()
    {
        var tuning = FakeAmdTuningApi.Valid();
        var backend = new AmdAdlxBackend(tuning);

        KnobWriteResult result = await backend.WriteAsync(AmdKnobs.PowerLimit, KnobValue.FromScalar(10d), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["power=50"], tuning.Writes);
        Assert.Equal(50d, result.Applied!.Value.Scalar);
    }

    [Fact]
    public async Task Write_ThatDoesNotStick_IsAFailure_NeverBlocked()
    {
        var tuning = FakeAmdTuningApi.Valid();
        tuning.PowerWriteSticks = false;
        var backend = new AmdAdlxBackend(tuning);

        KnobWriteResult result = await backend.WriteAsync(AmdKnobs.PowerLimit, KnobValue.FromScalar(110d), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.DriverBlocked);
        Assert.Equal(NvidiaNvapiBackend.VerifyFailedReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task Write_WhenTheDriverSaysNotSupported_IsBlocked()
    {
        var tuning = FakeAmdTuningApi.Valid();
        tuning.PowerWriteStatus = AdlxNotSupported;
        tuning.PowerWriteSticks = false;
        var backend = new AmdAdlxBackend(tuning);

        KnobWriteResult result = await backend.WriteAsync(AmdKnobs.PowerLimit, KnobValue.FromScalar(110d), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.DriverBlocked);
    }

    [Fact]
    public async Task Write_WhenTheDriverWantsAReset_SaysSo()
    {
        var tuning = FakeAmdTuningApi.Valid();
        tuning.PowerWriteStatus = AdlxResetNeeded;
        tuning.PowerWriteSticks = false;
        var backend = new AmdAdlxBackend(tuning);

        KnobWriteResult result = await backend.WriteAsync(AmdKnobs.PowerLimit, KnobValue.FromScalar(110d), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.DriverBlocked);
        Assert.Equal(AmdAdlxBackend.ResetNeededReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task Write_WhenTheGpuIsBusy_SaysSo()
    {
        var tuning = FakeAmdTuningApi.Valid();
        tuning.PowerWriteStatus = AdlxGpuInUse;
        tuning.PowerWriteSticks = false;
        var backend = new AmdAdlxBackend(tuning);

        KnobWriteResult result = await backend.WriteAsync(AmdKnobs.PowerLimit, KnobValue.FromScalar(110d), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AmdAdlxBackend.GpuInUseReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task Write_WithAnUnclassifiedStatus_Fails_WithoutInventingAReason()
    {
        var tuning = FakeAmdTuningApi.Valid();
        tuning.PowerWriteStatus = AdlxUnknown;
        tuning.PowerWriteSticks = false;
        var backend = new AmdAdlxBackend(tuning);

        KnobWriteResult result = await backend.WriteAsync(AmdKnobs.PowerLimit, KnobValue.FromScalar(110d), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.DriverBlocked);
        Assert.Equal(NvidiaNvapiBackend.RejectedReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task Write_WhenAdlxIsAbsent_Fails_WithTheRealReason_AndWritesNothing()
    {
        var tuning = new FakeAmdTuningApi { IsAvailable = false, UnavailableReasonKey = "overclock-adlx-missing" };
        var backend = new AmdAdlxBackend(tuning);

        KnobWriteResult result = await backend.WriteAsync(AmdKnobs.PowerLimit, KnobValue.FromScalar(110d), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("overclock-adlx-missing", result.FailureReasonKey);
        Assert.Empty(tuning.Writes);
    }

    [Fact]
    public async Task Write_WhenTheDriverGivesNoRange_Fails_AndWritesNothing()
    {
        var tuning = FakeAmdTuningApi.Valid();
        tuning.Device = new AmdTuningDevice(null, null, null, null);
        var backend = new AmdAdlxBackend(tuning);

        KnobWriteResult result = await backend.WriteAsync(AmdKnobs.PowerLimit, KnobValue.FromScalar(110d), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AmdAdlxBackend.RangeUnavailableReasonKey, result.FailureReasonKey);
        Assert.Empty(tuning.Writes);
    }

    [Theory]
    [InlineData("amd.gpu.coreClockOffset")]
    [InlineData("amd.gpu.memoryClockOffset")]
    [InlineData("nvidia.gpu.powerLimit")]
    public async Task Write_AndRead_OfAnythingButThePowerLimit_HasNoPublicSetter(string knobId)
    {
        var backend = new AmdAdlxBackend(FakeAmdTuningApi.Valid());

        KnobWriteResult written = await backend.WriteAsync(new KnobId(knobId), KnobValue.FromScalar(10d), CancellationToken.None);
        KnobRead read = await backend.ReadAsync(new KnobId(knobId), CancellationToken.None);

        // AMD clocks stay out on purpose: from Navi4 up the setter takes an
        // offset, before that an absolute frequency, and this module refuses to
        // guess which one a card expects.
        Assert.Equal(NvidiaNvapiBackend.NoPublicSetterReasonKey, written.FailureReasonKey);
        Assert.Equal(NvidiaNvapiBackend.NoPublicSetterReasonKey, read.FailureReasonKey);
    }

    [Fact]
    public async Task Read_ReturnsTheLivePercent()
    {
        var backend = new AmdAdlxBackend(FakeAmdTuningApi.Valid());

        KnobRead read = await backend.ReadAsync(AmdKnobs.PowerLimit, CancellationToken.None);

        Assert.True(read.Success);
        Assert.Equal(100d, read.Value.Scalar);
    }

    [Fact]
    public async Task Read_WhenTheDriverDoesNotAnswer_SaysSo()
    {
        var tuning = FakeAmdTuningApi.Valid();
        tuning.PowerPercent = null;
        var backend = new AmdAdlxBackend(tuning);

        KnobRead read = await backend.ReadAsync(AmdKnobs.PowerLimit, CancellationToken.None);

        Assert.False(read.Success);
        Assert.Equal("overclock-gpu-power-unavailable", read.FailureReasonKey);
    }

    [Fact]
    public async Task CanWrite_IsTrue_OnlyForThePowerLimit_WhenAdlxAnswers()
    {
        var backend = new AmdAdlxBackend(FakeAmdTuningApi.Valid());

        Assert.True(backend.CanWrite(AmdKnobs.PowerLimit));
        Assert.False(backend.CanWrite(new KnobId("amd.gpu.coreClockOffset")));
        Assert.Null(backend.BlockReasonKey(AmdKnobs.PowerLimit));
        Assert.Equal(
            NvidiaNvapiBackend.NoPublicSetterReasonKey,
            backend.BlockReasonKey(new KnobId("amd.gpu.coreClockOffset")));
    }

    [Fact]
    public async Task BlockReasonKey_ReportsTheRealBlocker_WhenTheDeviceIsGone()
    {
        var tuning = FakeAmdTuningApi.Valid();
        tuning.Device = null;
        var backend = new AmdAdlxBackend(tuning);

        Assert.False(backend.CanWrite(AmdKnobs.PowerLimit));
        Assert.Equal("overclock-adlx-device-unavailable", backend.BlockReasonKey(AmdKnobs.PowerLimit));
    }

    [Fact]
    public async Task Probe_WhenAdlxIsAbsent_ReportsNone_WithAReason()
    {
        var backend = new AmdAdlxBackend(new FakeAmdTuningApi { IsAvailable = false, UnavailableReasonKey = "overclock-adlx-missing" });

        OverclockCapabilities probe = backend.Probe(CancellationToken.None);

        Assert.Equal(GpuBackendKind.None, probe.Gpu);
        Assert.Contains(probe.Details, d => d.BackendId == OverclockCatalog.AmdAdlxBackendId && !d.Available);
    }

    [Fact]
    public async Task Probe_WhenAdlxAnswers_ClaimsExactlyTheAmdKnob()
    {
        var backend = new AmdAdlxBackend(FakeAmdTuningApi.Valid());

        OverclockCapabilities probe = backend.Probe(CancellationToken.None);

        Assert.Equal(GpuBackendKind.AmdAdlx, probe.Gpu);
        Assert.Contains(probe.Knobs, k => k.Id == AmdKnobs.PowerLimit);
        Assert.All(probe.Knobs, k => Assert.Equal(OverclockCatalog.AmdAdlxBackendId, k.BackendId));
        Assert.Equal(OverclockCatalog.AmdAdlxBackendId, backend.Id);
    }

    [Fact]
    public async Task Snapshot_StoresThePercent_Restore_PutsItBack()
    {
        var tuning = FakeAmdTuningApi.Valid();
        var backend = new AmdAdlxBackend(tuning);

        UnitSnapshot snapshot = await backend.SnapshotAsync(CancellationToken.None);

        Assert.Equal(1, snapshot.Count);
        tuning.PowerPercent = 80;
        RestoreOutcome outcome = await backend.RestoreAsync(snapshot, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.True(outcome.Verified);
        Assert.Equal(1, outcome.RestoredCount);
        Assert.Equal(["power=100"], tuning.Writes);
    }

    [Fact]
    public async Task Restore_WithAFailingWrite_ReportsIt_WithoutInventingSuccess()
    {
        var tuning = FakeAmdTuningApi.Valid();
        tuning.PowerWriteStatus = AdlxUnknown;
        tuning.PowerWriteSticks = false;
        var backend = new AmdAdlxBackend(tuning);

        var snapshot = new UnitSnapshot(
            OverclockComponent.Gpu,
            [new KeyValuePair<KnobId, KnobValue>(AmdKnobs.PowerLimit, KnobValue.FromScalar(100d))],
            DateTimeOffset.UnixEpoch,
            null);

        RestoreOutcome outcome = await backend.RestoreAsync(snapshot, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal(0, outcome.RestoredCount);
        Assert.Equal(AmdAdlxBackend.RestoreFailedReasonKey, outcome.FailureReasonKey);
    }

    [Fact]
    public async Task Restore_OfAnEmptySnapshot_IsVacuouslyVerified()
    {
        var backend = new AmdAdlxBackend(FakeAmdTuningApi.Valid());

        var snapshot = new UnitSnapshot(OverclockComponent.Gpu, [], DateTimeOffset.UnixEpoch, null);

        RestoreOutcome outcome = await backend.RestoreAsync(snapshot, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(0, outcome.RestoredCount);
    }

    internal sealed class FakeAmdTuningApi : IAmdTuningApi
    {
        public bool IsAvailable { get; set; } = true;

        public string? UnavailableReasonKey { get; set; }

        public AmdTuningDevice? Device { get; set; }

        public int? PowerPercent { get; set; }

        public int PowerWriteStatus { get; set; }

        public bool PowerWriteSticks { get; set; } = true;

        public List<string> Writes { get; } = [];

        public static FakeAmdTuningApi Valid() => new()
        {
            Device = new AmdTuningDevice(null, 50, 120, 100),
            PowerPercent = 100,
        };

        public bool TryDescribe(out AmdTuningDevice? device)
        {
            device = Device;
            return device is not null;
        }

        public int? ReadPowerLimitPercent() => PowerPercent;

        public int SetPowerLimitPercent(int percent)
        {
            Writes.Add($"power={percent}");
            if (PowerWriteSticks && PowerWriteStatus == 0)
            {
                PowerPercent = percent;
            }

            return PowerWriteStatus;
        }
    }
}
