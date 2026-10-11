using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Infrastructure.Overclock;
using CAO.Shared;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// A backend that never answers must degrade to a row, never hang the
/// section. NVML and WMI have no timeout of their own; the knob loop imposes
/// one per row (OverclockViewModel.DefaultRowTimeout).
/// </summary>
public sealed class OverclockRowTimeoutTests
{
    [Fact]
    public async Task Refresh_Completes_WhenABackendNeverAnswers()
    {
        var knob = OverclockCatalog.NvidiaGpuKnobs[0] with { BackendId = "test-hang" };
        var vm = OverclockTestHarness.Create(
            new SingleKnobRegistry(knob, "test-hang"),
            [new HangingBackend()],
            rowTimeout: TimeSpan.FromMilliseconds(300));

        await vm.RefreshCommand.ExecuteAsync(null);

        var row = Assert.Single(vm.GpuKnobs);
        Assert.Equal(OverclockControlState.DriverBlocked, row.State);
        Assert.False(row.IsActionable);
        Assert.NotEqual(OverclockViewModel.RowTimedOutReasonKey, row.Reason);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Refresh_RendersValues_WhenABackendIsSlowButFinite()
    {
        var knob = OverclockCatalog.NvidiaGpuKnobs[0] with { BackendId = "test-slow" };
        var vm = OverclockTestHarness.Create(
            new SingleKnobRegistry(knob, "test-slow"),
            [new SlowBackend()],
            rowTimeout: TimeSpan.FromSeconds(5));

        await vm.RefreshCommand.ExecuteAsync(null);

        var row = Assert.Single(vm.GpuKnobs);
        Assert.True(row.IsActionable);
        Assert.Equal("110 %", row.CurrentValue);
        Assert.False(vm.IsBusy);
    }

    private sealed class SingleKnobRegistry(OverclockKnob knob, string backendId) : IOverclockBackendRegistry
    {
        public OverclockCapabilities ProbeAll(CancellationToken ct) => new(
            CpuBackendKind.None,
            GpuBackendKind.None,
            MemoryBackendKind.None,
            [new BackendAvailability(
                BackendId: backendId,
                Available: true,
                UnavailableReasonKey: string.Empty,
                RemedyHintKey: null,
                RequiresRestart: false,
                DriverVersion: null,
                LastProbeEvidence: EvidenceLevel.Vendor)],
            [knob]);
    }

    private sealed class HangingBackend : IOverclockBackend, IOverclockWriteGate
    {
        public string Id => "test-hang";

        public OverclockCapabilities Probe(CancellationToken ct) => OverclockCapabilities.Empty;

        public bool CanWrite(KnobId knob) => true;

        public string? BlockReasonKey(KnobId knob) => null;

        public async ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return KnobRead.Fail("unreachable");
        }

        public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct) =>
            new(KnobWriteResult.Fail("test-no-write"));

        public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) =>
            new(new UnitSnapshot(OverclockComponent.Gpu, [], DateTimeOffset.UtcNow, null));

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) =>
            new(new RestoreOutcome(true, 0, true, null));
    }

    private sealed class SlowBackend : IOverclockBackend, IOverclockWriteGate
    {
        public string Id => "test-slow";

        public OverclockCapabilities Probe(CancellationToken ct) => OverclockCapabilities.Empty;

        public bool CanWrite(KnobId knob) => true;

        public string? BlockReasonKey(KnobId knob) => null;

        public async ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct)
        {
            await Task.Delay(100, ct);
            return KnobRead.Ok(KnobValue.FromScalar(110));
        }

        public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct) =>
            new(KnobWriteResult.Fail("test-no-write"));

        public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) =>
            new(new UnitSnapshot(OverclockComponent.Gpu, [], DateTimeOffset.UtcNow, null));

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) =>
            new(new RestoreOutcome(true, 0, true, null));
    }
}
