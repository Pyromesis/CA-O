using CAO.Core.Overclock;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Reporting;
using CAO.Core.Overclock.Stability;
using CAO.Core.Overclock.Gaming;
using CAO.Infrastructure.Overclock.Gaming;
using CAO.Infrastructure.Overclock;
using CAO.Infrastructure.Overclock.Reporting;
using CAO.UI.ViewModels;
using CAO.Shared;

namespace CAO.UI.Tests;

/// <summary>
/// Builds an <see cref="OverclockViewModel"/> whose every out-of-process door
/// is stubbed: the checkpoint store, the boot probe, the tuning target and the
/// ceiling source.
///
/// This exists because the page reaches for four different things - the backend
/// registry, the delta source, the recovery engine and the ceiling source - and
/// the last two default to real hardware. A test that injects only the registry
/// ends up P/Invoking into nvml.dll and asking WMI for the boot time from a
/// test host, which is not a test any more.
/// </summary>
internal static class OverclockTestHarness
{
    public static readonly DateTimeOffset BootUtc = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    public static OverclockViewModel Create(
        IOverclockBackendRegistry registry,
        IReadOnlyList<IOverclockBackend> backends,
        IGameOverclockSource? gameSource = null,
        IOverclockDeltaSource? deltaSource = null,
        IOverclockDeltaStore? store = null,
        IReadOnlyList<TuningCeiling>? ceilings = null,
        IReadOnlyList<TuningCheckpoint>? openRuns = null,
        ITuningTarget? target = null)
    {
        target ??= new HarnessTarget();
        var checkpoints = new HarnessCheckpointStore(openRuns ?? []);

        var recovery = new OverclockRecoveryService(
            checkpoints,
            new HarnessBootProbe(),
            new HarnessResolver(target));

        return new OverclockViewModel(
            registry,
            backends,
            deltaSource: deltaSource,
            store: store,
            recovery: recovery,
            ceilingSource: new HarnessCeilingSource(ceilings ?? []),
            gameSource: gameSource ?? new HarnessGameSource());
    }

    /// <summary>Builds a view model over an empty registry: no knobs, no backends.</summary>
    public static OverclockViewModel Empty() =>
        Create(new OverclockReadOnlyRegistry(), []);

    /// <summary>
    /// Stands in for the game source. It exists because the real one walks the process
    /// list and reads the service registry, and a test that lets it do that is testing
    /// the machine instead of the page.
    /// </summary>
    private sealed class HarnessGameSource : IGameOverclockSource
    {
        public bool ScanSucceeded { get; init; } = true;

        public IReadOnlyList<GameOverclockEntry> Entries { get; init; } = [];

        public ValueTask<GameOverclockSnapshot> ReadAsync(CancellationToken ct) =>
            new(new GameOverclockSnapshot(ScanSucceeded, [], Entries));
    }

    private sealed class HarnessBootProbe : IBootHealthProbe
    {
        public Task<BootHealthReport> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new BootHealthReport(false, BootUtc, "harness"));
    }

    private sealed class HarnessResolver(ITuningTarget target) : ITuningTargetResolver
    {
        public bool TryResolve(string backendId, out ITuningTarget? resolved)
        {
            resolved = backendId == target.BackendId ? target : null;
            return resolved is not null;
        }
    }

    private sealed class HarnessTarget : ITuningTarget
    {
        private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);

        public string BackendId => "harness-backend";

        public List<string> AppliedWrites { get; } = [];

        public double? Peek(string knobId) => _values.TryGetValue(knobId, out var v) ? v : null;

        public ValueTask<UnitSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new UnitSnapshot(OverclockComponent.Gpu, [], BootUtc, "999.99"));

        public ValueTask<KnobWriteResult> ApplyAsync(KnobId id, KnobValue value, CancellationToken cancellationToken) =>
            Write(id, value, cancellationToken);

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new RestoreOutcome(true, snapshot.Count, true, null));

        public ValueTask<KnobWriteResult> RestoreValueAsync(KnobId id, KnobValue value, CancellationToken cancellationToken) =>
            Write(id, value, cancellationToken);

        private ValueTask<KnobWriteResult> Write(KnobId id, KnobValue value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values[id.Value] = value.Scalar;
            AppliedWrites.Add(value.Scalar.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            return ValueTask.FromResult(KnobWriteResult.Ok(value));
        }
    }

    private sealed class HarnessCheckpointStore(IReadOnlyList<TuningCheckpoint> initial) : ITuningCheckpointStore
    {
        private readonly Dictionary<Guid, TuningCheckpoint> _items = initial.ToDictionary(c => c.RunId);

        public Task SaveAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            _items[checkpoint.RunId] = checkpoint;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TuningCheckpoint>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TuningCheckpoint>>(_items.Values.ToList());

        public Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.Remove(runId));

        public Task<string?> QuarantineAsync(Guid runId, string reasonKey, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class HarnessCeilingSource(IReadOnlyList<TuningCeiling> ceilings) : IOverclockCeilingSource
    {
        public ValueTask<IReadOnlyList<TuningCeiling>> ReadAsync(CancellationToken ct) => new(ceilings);
    }
}