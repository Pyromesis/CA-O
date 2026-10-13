using CAO.Core.Abstractions;
using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Reporting;
using CAO.Core.Overclock.Stability;
using CAO.Infrastructure.Overclock;
using CAO.Shared;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// Puerta de punto de restauración: sin punto no se escribe nada en ninguna
/// de las tres vías (perfil, knob, auto). El servicio es un falso inyectado;
/// el WMI real nunca se toca desde el host de tests. Un fallo, una excepción
/// o una cancelación bloquean igual: ni journal, ni checkpoints nuevos, ni
/// backend.Write.
/// </summary>
public sealed class OverclockRestorePointGateTests
{
    [Fact]
    public async Task Gate_Open_LetsProfileApply_Write()
    {
        var (vm, backend, _, _) = Build(writable: true, restore: FakeRestore.Allow());

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.ApplyProfileCommand.ExecuteAsync(null);

        Assert.NotEmpty(backend.Writes);
        Assert.NotEqual("overclock-profile-summary", vm.ApplySummary);
    }

    [Fact]
    public async Task Gate_Closed_BlocksProfileApply_WithZeroWrites()
    {
        var restore = FakeRestore.Deny("WMI sin respuesta");
        var (vm, backend, journal, checkpoints) = Build(writable: true, restore: restore);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.ApplyProfileCommand.ExecuteAsync(null);

        Assert.Single(restore.Descriptions);
        Assert.StartsWith("CA-O overclock", restore.Descriptions[0], StringComparison.Ordinal);
        Assert.Empty(backend.Writes);
        Assert.Empty(journal.Phases);
        Assert.Empty(checkpoints.Items);
        Assert.NotEqual("overclock-restore-point-blocked", vm.ApplySummary);
        Assert.Equal(vm.ApplySummary, vm.StatusLine);
        Assert.Contains("WMI sin respuesta", vm.ApplySummary, StringComparison.Ordinal);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Gate_Closed_BlocksSingleTuning_WithZeroWrites()
    {
        var restore = FakeRestore.Deny("sin punto");
        var (vm, backend, stores) = BuildTuning(writable: true, restore: restore);
        var knobId = OverclockCatalog.CpuPolicyKnobs[0].Id.Value;

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.StartTuningCommand.ExecuteAsync(knobId);

        Assert.NotEmpty(restore.Descriptions);
        Assert.Empty(backend.Writes);
        Assert.Empty(stores.Journal.Phases);
        Assert.Empty(stores.CheckpointStore.Items);
        Assert.Empty(stores.Profiles);
        Assert.NotEqual("overclock-restore-point-blocked", vm.StatusLine);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Gate_Closed_BlocksAutoTuning_WithZeroWrites()
    {
        var restore = FakeRestore.Deny("sin punto");
        var (vm, backend, stores) = BuildTuning(writable: true, restore: restore);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.StartAutoTuningCommand.ExecuteAsync(null);

        Assert.NotEmpty(restore.Descriptions);
        Assert.Empty(backend.Writes);
        Assert.Empty(stores.Journal.Phases);
        Assert.Empty(stores.Profiles);
        Assert.NotEqual("overclock-restore-point-blocked", vm.ApplySummary);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Gate_Cancelled_BlocksProfileApply_WithZeroWrites()
    {
        var restore = FakeRestore.Cancel();
        var (vm, backend, journal, checkpoints) = Build(writable: true, restore: restore);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.ApplyProfileCommand.ExecuteAsync(null);

        Assert.Empty(backend.Writes);
        Assert.Empty(journal.Phases);
        Assert.Empty(checkpoints.Items);
        Assert.NotEqual("overclock-restore-point-blocked", vm.ApplySummary);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Gate_Throwing_BlocksProfileApply_WithZeroWrites()
    {
        var restore = FakeRestore.Throw(new TimeoutException("techo WMI"));
        var (vm, backend, journal, checkpoints) = Build(writable: true, restore: restore);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.ApplyProfileCommand.ExecuteAsync(null);

        Assert.Empty(backend.Writes);
        Assert.Empty(journal.Phases);
        Assert.Empty(checkpoints.Items);
        Assert.NotEqual("overclock-restore-point-blocked", vm.ApplySummary);
        Assert.False(vm.IsBusy);
    }

    private static (OverclockViewModel ViewModel, GateBackend Backend, RecordingJournal Journal, SinkCheckpointStore Checkpoints) Build(
        bool writable, IRestorePointService restore)
    {
        var backend = new GateBackend(writable);
        var journal = new RecordingJournal();
        var checkpoints = new SinkCheckpointStore();
        var vm = OverclockTestHarness.Create(
            new OverclockReadOnlyRegistry(backend),
            [backend],
            checkpointStore: checkpoints,
            journal: journal,
            restorePointService: restore);
        return (vm, backend, journal, checkpoints);
    }

    private static (OverclockViewModel ViewModel, GateBackend Backend, GateTuningStores Stores) BuildTuning(
        bool writable, IRestorePointService restore)
    {
        var backend = new GateBackend(writable);
        var stores = new GateTuningStores();
        var vm = OverclockTestHarness.Create(
            new OverclockReadOnlyRegistry(backend),
            [backend],
            probe: new ScriptedProbe(),
            profileStore: stores.ProfileStore,
            checkpointStore: stores.CheckpointStore,
            journal: stores.Journal,
            restorePointService: restore);
        return (vm, backend, stores);
    }

    private sealed class FakeRestore(Func<CancellationToken, Task<(bool Success, string ReasonEs)>> behavior)
        : IRestorePointService
    {
        public List<string> Descriptions { get; } = [];

        public static FakeRestore Allow() =>
            new(_ => Task.FromResult((true, "falso")));

        public static FakeRestore Deny(string reason) =>
            new(_ => Task.FromResult((false, reason)));

        public static FakeRestore Cancel() =>
            new(ct => Task.FromException<(bool, string)>(new OperationCanceledException(ct)));

        public static FakeRestore Throw(Exception ex) =>
            new(_ => Task.FromException<(bool, string)>(ex));

        public async Task<(bool Success, string ReasonEs)> CreateAsync(string description, CancellationToken ct = default)
        {
            Descriptions.Add(description);
            return await behavior(ct).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<RestorePointInfo>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RestorePointInfo>>([]);
    }

    private sealed class RecordingJournal : IOverclockJournal
    {
        public List<TransactionPhase> Phases { get; } = [];

        public ValueTask WriteAsync(TransactionPhase phase, string? detail, CancellationToken cancellationToken)
        {
            Phases.Add(phase);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SinkCheckpointStore : ITuningCheckpointStore
    {
        public readonly Dictionary<Guid, TuningCheckpoint> Items = new();

        public Task SaveAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            Items[checkpoint.RunId] = checkpoint;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TuningCheckpoint>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TuningCheckpoint>>(Items.Values.ToList());

        public Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Remove(runId));

        public Task<string?> QuarantineAsync(Guid runId, string reasonKey, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class GateTuningStores
    {
        public List<ValidatedOverclockProfile> Profiles { get; } = [];

        public RecordingJournal Journal { get; } = new();

        public SinkCheckpointStore CheckpointStore { get; } = new();

        public IValidatedProfileStore ProfileStore => new SinkProfileStore(Profiles);

        private sealed class SinkProfileStore(List<ValidatedOverclockProfile> sink) : IValidatedProfileStore
        {
            public Task<IReadOnlyList<ValidatedOverclockProfile>> ListAsync(CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<ValidatedOverclockProfile>>(sink.ToList());

            public Task SaveAsync(ValidatedOverclockProfile profile, CancellationToken cancellationToken)
            {
                sink.Add(profile);
                return Task.CompletedTask;
            }
        }
    }

    private sealed class ScriptedProbe : IStabilityProbe
    {
        public string Id => "scripted-gate-probe";

        public IReadOnlyList<ProbeMetric> SupportedMetrics => [ProbeMetric.EffectiveClockStall];

        public Task<ProbeRun> RunAsync(ProbeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ProbeRun(
                Completed: true,
                Aborted: false,
                [new ProbeSample(DateTimeOffset.UnixEpoch, [])],
                null));
    }

    private sealed class GateBackend : IOverclockBackend, IOverclockWriteGate
    {
        private readonly bool _writable;
        private readonly Dictionary<string, KnobValue> _current = new(StringComparer.Ordinal);

        public GateBackend(bool writable)
        {
            _writable = writable;
            foreach (var knob in OverclockCatalog.CpuPolicyKnobs)
            {
                _current[knob.Id.Value] = knob.Range.Allowed is { Count: > 0 }
                    ? KnobValue.FromIndex(0)
                    : KnobValue.FromScalar(80d);
            }
        }

        public List<(KnobId Id, KnobValue Value)> Writes { get; } = [];

        public string Id => OverclockCatalog.IntelPolicyBackendId;

        public OverclockCapabilities Probe(CancellationToken ct) => new(
            CpuBackendKind.PolicyOnly,
            GpuBackendKind.None,
            MemoryBackendKind.None,
            [new BackendAvailability(Id, true, string.Empty, null, false, "fake", EvidenceLevel.Vendor)],
            OverclockCatalog.CpuPolicyKnobs);

        public bool CanWrite(KnobId knob) => _writable;

        public string? BlockReasonKey(KnobId knob) => _writable ? null : "overclock-fake-write-blocked";

        public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct) =>
            ValueTask.FromResult(
                _current.TryGetValue(id.Value, out var value)
                    ? KnobRead.Ok(value)
                    : KnobRead.Fail("overclock-fake-read-disabled"));

        public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct)
        {
            if (!_writable)
            {
                return ValueTask.FromResult(KnobWriteResult.Blocked("overclock-fake-write-blocked"));
            }

            if (!_current.ContainsKey(id.Value))
            {
                return ValueTask.FromResult(KnobWriteResult.Fail("overclock-fake-unknown-knob"));
            }

            Writes.Add((id, value));
            _current[id.Value] = value;
            return ValueTask.FromResult(KnobWriteResult.Ok(value));
        }

        public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) =>
            ValueTask.FromResult(new UnitSnapshot(
                OverclockComponent.Cpu,
                _current.Select(kv => new KeyValuePair<KnobId, KnobValue>(new KnobId(kv.Key), kv.Value)).ToList(),
                DateTimeOffset.UnixEpoch,
                "fake"));

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct)
        {
            foreach (var entry in snapshot.Values)
            {
                _current[entry.Key.Value] = entry.Value;
            }

            return ValueTask.FromResult(new RestoreOutcome(true, snapshot.Count, true, null));
        }
    }
}
