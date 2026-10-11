using CAO.Core.Overclock;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Reporting;
using CAO.Infrastructure.Overclock;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

/// <summary>
/// The adapters that let the tuning engine and the recovery service talk to the
/// real backends without a second write path. Every test drives the real adapter
/// and fakes only the outermost hardware-facing dependency, because a fake that
/// reimplemented the adapter's own logic would prove nothing.
/// </summary>
public sealed class OverclockRecoveryAdapterTests
{
    private static readonly UnitSnapshot EmptySnapshot =
        new(OverclockComponent.Gpu, Array.Empty<KeyValuePair<KnobId, KnobValue>>(), DateTimeOffset.UnixEpoch, "fake");

    // ---------------------------------------------------------------- boot health

    [Fact]
    public async Task BootProbe_NeverThrows_WhateverWmiAnswers()
    {
        var report = await new SystemBootHealthProbe().GetAsync(CancellationToken.None);

        // The only invariant that must hold on every machine: a clean report
        // carries the boot instant. Without it the temporal cut in the recovery
        // service silently degrades to "everything is clean".
        if (report.LastBootWasClean)
        {
            Assert.NotNull(report.LastBootUtc);
        }
        else
        {
            Assert.Equal(SystemBootHealthProbe.UnavailableReasonKey, report.ReasonKey);
        }
    }

    [Fact]
    public async Task BootProbe_HonoursCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SystemBootHealthProbe().GetAsync(cts.Token));
    }

    // ---------------------------------------------------------------- target adapter

    [Fact]
    public async Task Apply_ReadsFirst_SoAnUnreadableSettingIsNeverWrittenBlind()
    {
        var backend = new RecordingBackend { ReadSucceeds = false };
        var target = new BackendTuningTarget(backend);

        var result = await target.ApplyAsync(NvKnobs.PowerLimit, KnobValue.FromScalar(120d), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("fake-read-failed", result.FailureReasonKey);
        Assert.Empty(backend.Writes);
    }

    [Fact]
    public async Task Apply_FallsBackToItsOwnReason_WhenTheReadGaveNone()
    {
        var backend = new RecordingBackend { ReadSucceeds = false, ReadReasonKey = null };

        var result = await new BackendTuningTarget(backend)
            .ApplyAsync(NvKnobs.PowerLimit, KnobValue.FromScalar(120d), CancellationToken.None);

        Assert.Equal(BackendTuningTarget.ReadFailedReasonKey, result.FailureReasonKey);
    }

    [Fact]
    public async Task Apply_WritesOnce_WhenTheSettingIsReadable()
    {
        var backend = new RecordingBackend { ReadSucceeds = true };

        var result = await new BackendTuningTarget(backend)
            .ApplyAsync(NvKnobs.CoreClockOffset, KnobValue.FromScalar(120d), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["120"], backend.Writes);
    }

    [Fact]
    public async Task CaptureAndRestore_DelegateToTheSameBackendMethods()
    {
        var backend = new RecordingBackend();
        var target = new BackendTuningTarget(backend);

        await target.CaptureAsync(CancellationToken.None);
        await target.RestoreAsync(EmptySnapshot, CancellationToken.None);
        await target.RestoreValueAsync(NvKnobs.PowerLimit, KnobValue.FromScalar(90d), CancellationToken.None);

        Assert.Equal(1, backend.SnapshotCalls);
        Assert.Equal(1, backend.RestoreCalls);
        // The recovery after a reboot has a scalar, not a snapshot: it must land
        // on the same write path the tuning run used or the two can disagree.
        Assert.Equal(["90"], backend.Writes);
    }

    [Fact]
    public void Target_ReportsTheBackendId_AndRejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => new BackendTuningTarget(null!));

    // ---------------------------------------------------------------- resolver

    [Fact]
    public void Resolver_ResolvesByBackendId() =>
        Assert.True(new BackendTuningTargetResolver([new RecordingBackend("a")]).TryResolve("a", out var target));

    [Fact]
    public void Resolver_RefusesAnUnknownId()
    {
        Assert.False(new BackendTuningTargetResolver([new RecordingBackend("a")]).TryResolve("b", out var target));
        Assert.Null(target);
    }

    [Fact]
    public async Task Resolver_KeepsTheFirstBackend_WhenTwoShareAnId()
    {
        var first = new RecordingBackend("dup") { ReadSucceeds = true };
        var second = new RecordingBackend("dup") { ReadSucceeds = true };

        Assert.True(new BackendTuningTargetResolver([first, second]).TryResolve("dup", out var target));
        await target!.ApplyAsync(NvKnobs.PowerLimit, KnobValue.FromScalar(100d), CancellationToken.None);

        // Recovery has to land on the backend the tuning run used, not on the
        // last one registered under the same id.
        Assert.Equal(["100"], first.Writes);
        Assert.Empty(second.Writes);
    }

    [Fact]
    public void Resolver_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => new BackendTuningTargetResolver(null!));

    // ---------------------------------------------------------------- ceilings

    [Fact]
    public async Task Ceilings_AreEmpty_WhenThereIsNoWritePath()
    {
        var source = new NvidiaTuningCeilingSource(new FakeTuningApi { IsAvailable = false });

        Assert.Empty(await source.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Ceilings_KeepTheReason_WhenTheDriverDoesNotAnswer()
    {
        var source = new NvidiaTuningCeilingSource(new FakeTuningApi { IsAvailable = true, Device = null });

        var rows = await source.ReadAsync(CancellationToken.None);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Null(row.Min);
            Assert.Null(row.Max);
            Assert.Equal(NvidiaTuningCeilingSource.UnavailableReasonKey, row.ReasonKey);
        });
    }

    [Fact]
    public async Task Ceilings_OnlyCoverTheSettingsThatCanActuallyBeWritten()
    {
        var rows = await Source(Device()).ReadAsync(CancellationToken.None);

        Assert.Equal(3, rows.Count);
        Assert.DoesNotContain(rows, r => r.Knob == NvKnobs.ThermalLimit);
        Assert.DoesNotContain(rows, r => r.Knob == NvKnobs.FanTarget);
        Assert.DoesNotContain(rows, r => r.Knob == NvKnobs.ClockReadout);
    }

    [Fact]
    public async Task PowerCeiling_ExpressedAsPercentOfTheDefaultLimit()
    {
        // default 300 mW, driver range 100..600 mW => 33.33 % .. 200 %.
        var row = Power(await Source(Device(defaultMilliwatts: 300)).ReadAsync(CancellationToken.None));

        Assert.Equal(33.33d, row.Min!.Value, 2);
        Assert.Equal(200d, row.Max!.Value, 2);
    }

    [Fact]
    public async Task PowerCeiling_FallsBackToTheHardwareMinimum_WhenThereIsNoDefault()
    {
        // min 100 mW becomes the anchor => the floor sits at 100 % and the
        // advertised ceiling reads as 600 % of that anchor.
        var row = Power(await Source(Device(defaultMilliwatts: null, minMilliwatts: 100)).ReadAsync(CancellationToken.None));

        Assert.Equal(100d, row.Min!.Value, 2);
        Assert.Equal(600d, row.Max!.Value, 2);
    }

    [Fact]
    public async Task PowerCeiling_SaysSo_WhenTheDriverEnforcesLessThanItAdvertises()
    {
        var row = Power(await Source(Device(enforcedMilliwatts: 400)).ReadAsync(CancellationToken.None));

        Assert.Equal(NvidiaTuningCeilingSource.EnforcedBelowMaxReasonKey, row.ReasonKey);
    }

    [Fact]
    public async Task PowerCeiling_StaysSilent_WhenTheEnforcedLimitIsNotLower()
    {
        var row = Power(await Source(Device(enforcedMilliwatts: 600)).ReadAsync(CancellationToken.None));

        Assert.Null(row.ReasonKey);
    }

    [Fact]
    public async Task PowerCeiling_ReportsNoAnchorInsteadOfInventingOne()
    {
        var device = Device(defaultMilliwatts: null, minMilliwatts: null);
        var row = Power(await Source(device).ReadAsync(CancellationToken.None));

        Assert.Null(row.Min);
        Assert.Null(row.Max);
        Assert.Equal(NvidiaTuningCeilingSource.AnchorUnavailableReasonKey, row.ReasonKey);
    }

    [Fact]
    public async Task OffsetCeiling_ConvertMegahertzToTheCatalogsHertz()
    {
        var rows = await Source(Device(coreMin: -500, coreMax: 500)).ReadAsync(CancellationToken.None);

        var core = rows.Single(r => r.Knob == NvKnobs.CoreClockOffset);
        Assert.Equal(-500_000_000d, core.Min);
        Assert.Equal(500_000_000d, core.Max);
        Assert.Equal(NvidiaTuningCeilingSource.HertzUnitKey, core.Unit);
    }

    [Fact]
    public async Task OffsetCeiling_ReportsTheReason_WhenTheDriverGivesNoRange()
    {
        var rows = await Source(Device(coreMin: null)).ReadAsync(CancellationToken.None);

        var core = rows.Single(r => r.Knob == NvKnobs.CoreClockOffset);
        Assert.Null(core.Min);
        Assert.Equal(NvidiaTuningCeilingSource.UnavailableReasonKey, core.ReasonKey);
    }

    private static NvidiaTuningCeilingSource Source(NvidiaTuningDevice device) =>
        new(new FakeTuningApi { IsAvailable = true, Device = device });

    private static TuningCeiling Power(IReadOnlyList<TuningCeiling> rows) =>
        rows.Single(r => r.Knob == NvKnobs.PowerLimit);

    private static NvidiaTuningDevice Device(
        int? minMilliwatts = 100,
        int? maxMilliwatts = 600,
        int? defaultMilliwatts = 300,
        int? enforcedMilliwatts = null,
        int? coreMin = -500,
        int? coreMax = 500,
        int? memoryMin = -500,
        int? memoryMax = 1500) =>
        new("580.68", minMilliwatts, maxMilliwatts, defaultMilliwatts, enforcedMilliwatts, coreMin, coreMax, memoryMin, memoryMax);
}

/// <summary>A backend that records what was asked of it and answers on demand.</summary>
internal sealed class RecordingBackend(string id = "fake") : IOverclockBackend
{
    public string Id { get; } = id;

    public bool ReadSucceeds { get; set; }

    public string? ReadReasonKey { get; set; } = "fake-read-failed";

    public List<string> Writes { get; } = [];

    public int SnapshotCalls { get; private set; }

    public int RestoreCalls { get; private set; }

    public OverclockCapabilities Probe(CancellationToken ct) => OverclockCapabilities.Empty;

    public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct)
    {
        if (ReadSucceeds)
        {
            return new ValueTask<KnobRead>(KnobRead.Ok(KnobValue.FromScalar(100d)));
        }

        // KnobRead.Fail refuses a null reason, so the only way a backend can hand
        // one back is through the positional record. That is exactly the shape the
        // adapter's fallback exists for, so the fake has to be able to build it.
        return new ValueTask<KnobRead>(ReadReasonKey is null
            ? new KnobRead(false, KnobValue.FromScalar(0d), null)
            : KnobRead.Fail(ReadReasonKey));
    }

    public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct)
    {
        Writes.Add(value.Scalar.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        return ValueTask.FromResult(KnobWriteResult.Ok(value));
    }

    public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct)
    {
        SnapshotCalls++;
        return new ValueTask<UnitSnapshot>(EmptySnapshotOf());
    }

    public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct)
    {
        RestoreCalls++;
        return new ValueTask<RestoreOutcome>(new RestoreOutcome(true, snapshot.Count, true, null));
    }

    private static UnitSnapshot EmptySnapshotOf() =>
        new(OverclockComponent.Gpu, Array.Empty<KeyValuePair<KnobId, KnobValue>>(), DateTimeOffset.UnixEpoch, "fake");
}

/// <summary>A tuning API that answers from a canned device description.</summary>
internal sealed class FakeTuningApi : INvidiaTuningApi
{
    public bool IsAvailable { get; set; } = true;

    public string? UnavailableReasonKey { get; set; }

    public NvidiaTuningDevice? Device { get; set; }

    public bool TryDescribe(out NvidiaTuningDevice? device)
    {
        device = Device;
        return device is not null;
    }

    public int? ReadPowerLimitMilliwatts() => Device?.DefaultPowerLimitMilliwatts;

    public int? ReadCoreOffsetMegahertz() => 0;

    public int? ReadMemoryOffsetMegahertz() => 0;

    public int SetPowerLimitMilliwatts(int milliwatts) => 0;

    public int SetCoreOffsetMegahertz(int megahertz) => 0;

    public int SetMemoryOffsetMegahertz(int megahertz) => 0;
}