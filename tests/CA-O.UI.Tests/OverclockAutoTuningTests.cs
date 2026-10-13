using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Reporting;
using CAO.Core.Overclock.Stability;
using CAO.Infrastructure.Overclock;
using CAO.Infrastructure.Overclock.Gaming;
using CAO.Shared;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// Overclock automático con propuesta y tarjeta de perfil solo-accionable: el auto
/// recorre cada knob accionable con el mismo motor de bisección del ajuste manual,
/// guarda los perfiles validados y PROPONE (resumen + botón Aplicar al usuario,
/// nunca un apply silencioso). La tarjeta 00 PERFIL solo lista escribibles.
/// Fakes inyectados en el ViewModel real vía harness.
/// </summary>
public sealed class OverclockAutoTuningTests
{
    [Fact]
    public async Task AutoTuning_ProposesWithoutApplying_PresetApplyNeverRuns()
    {
        var (vm, stores) = BuildAuto(writable: true);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.StartAutoTuningCommand.ExecuteAsync(null);

        var profile = Assert.Single(stores.Profiles);
        Assert.Equal(new KnobId("amd.gpu.powerLimit"), profile.Knob);
        Assert.Contains(stores.Journal.Phases, p => p == TransactionPhase.OverclockStarted);
        Assert.Contains(stores.Journal.Phases, p => p == TransactionPhase.OverclockStepApplied);

        // La vía de presets (ApplyOneAsync) es la única que registra detalles
        // "knob=...": el motor escribe "id=valor". Si aparece un detalle knob=,
        // el auto aplicó en silencio y el test debe caer.
        Assert.DoesNotContain(stores.Journal.Details, d => d is not null && d.StartsWith("knob=", StringComparison.Ordinal));

        Assert.NotEqual("overclock-auto-proposal", vm.ApplySummary);
        Assert.Equal(vm.ApplySummary, vm.StatusLine);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task AutoTuning_SkipsNonActionableKnobs_AndSaysSo()
    {
        var (vm, stores) = BuildAuto(writable: false);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(vm.ProfileKnobs);

        await vm.StartAutoTuningCommand.ExecuteAsync(null);

        Assert.Empty(stores.Profiles);
        Assert.Empty(stores.Journal.Phases);
        Assert.NotEqual("overclock-profile-no-actionable", vm.ApplySummary);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task AutoTuning_BlockedByAntiCheat_WritesNothing()
    {
        var backend = new TuningBackend(writable: true);
        var stores = new TuningStores();
        var vm = OverclockTestHarness.Create(
            new OverclockReadOnlyRegistry(backend),
            [backend],
            gameSource: new AntiCheatGameSource(),
            probe: new ScriptedProbe(),
            profileStore: stores.ProfileStore,
            checkpointStore: stores.CheckpointStore,
            journal: stores.Journal);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.StartAutoTuningCommand.ExecuteAsync(null);

        Assert.Empty(stores.Profiles);
        Assert.Empty(stores.Journal.Phases);
        Assert.NotEqual("overclock-auto-blocked-anticheat", vm.ApplySummary);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task AutoTuning_BlockedByPendingRecovery_WritesNothing()
    {
        var backend = new TuningBackend(writable: true);
        var stores = new TuningStores();
        var open = new TuningCheckpoint
        {
            RunId = Guid.NewGuid(),
            BackendId = "ghost-backend",
            Knob = "amd.gpu.powerLimit",
            Component = nameof(OverclockComponent.Gpu),
            AppliedValue = 110d,
            LastKnownGoodValue = 100d,
            Phase = CheckpointPhase.Started,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
        };
        var vm = OverclockTestHarness.Create(
            new OverclockReadOnlyRegistry(backend),
            [backend],
            openRuns: [open],
            probe: new ScriptedProbe(),
            profileStore: stores.ProfileStore,
            checkpointStore: stores.CheckpointStore,
            journal: stores.Journal);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.PendingRecoveries);

        await vm.StartAutoTuningCommand.ExecuteAsync(null);

        Assert.Empty(stores.Profiles);
        Assert.NotEqual("overclock-auto-blocked-recovery", vm.ApplySummary);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task AutoTuning_Cancel_LeavesCleanState_AndResumes()
    {
        var backend = new BlockingTuningBackend();
        var stores = new TuningStores();
        var vm = OverclockTestHarness.Create(
            new OverclockReadOnlyRegistry(backend),
            [backend],
            probe: new ScriptedProbe(),
            profileStore: stores.ProfileStore,
            checkpointStore: stores.CheckpointStore,
            journal: stores.Journal);

        await vm.RefreshCommand.ExecuteAsync(null);

        var run = vm.StartAutoTuningCommand.ExecuteAsync(null);
        Assert.True(backend.Entered.Wait(TimeSpan.FromSeconds(5)));
        vm.StartAutoTuningCommand.Cancel();
        await run;

        Assert.False(vm.IsBusy);
        Assert.Empty(stores.Profiles);
        Assert.NotEqual("overclock_cancelled", vm.StatusLine);

        backend.Release = true;
        await vm.StartAutoTuningCommand.ExecuteAsync(null);

        Assert.Single(stores.Profiles);
        Assert.NotEqual("overclock-auto-proposal", vm.ApplySummary);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ProfileCard_ListsOnlyActionableKnobs()
    {
        var amd = new TuningBackend(writable: true);
        var cpu = new CpuBlockedBackend();
        var stores = new TuningStores();
        var vm = OverclockTestHarness.Create(
            new OverclockReadOnlyRegistry(amd, cpu),
            [amd, cpu],
            probe: new ScriptedProbe(),
            profileStore: stores.ProfileStore,
            checkpointStore: stores.CheckpointStore,
            journal: stores.Journal);

        await vm.RefreshCommand.ExecuteAsync(null);

        var total = vm.CpuKnobs.Count + vm.GpuKnobs.Count + vm.MemoryKnobs.Count;
        Assert.True(total > vm.ProfileKnobs.Count);
        Assert.NotEmpty(vm.ProfileKnobs);
        Assert.All(vm.ProfileKnobs, r => Assert.True(r.IsActionable));
        Assert.Equal(vm.TuningTargets.Count, vm.ProfileKnobs.Count);
    }

    [Fact]
    public void ThePage_WiresAutoTuning_AndFoldsTelemetryOutsideTheProfileCard()
    {
        var xaml = ReadRepoFile("src", "CA-O.UI", "OverclockPage.xaml");

        Assert.Contains("ViewModel.StartAutoTuningCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.ProfileKnobs", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.AutoTuneButtonLabel", xaml, StringComparison.Ordinal);
        Assert.Contains("<Expander", xaml, StringComparison.Ordinal);

        var cardStart = xaml.IndexOf("00  PERFIL", StringComparison.Ordinal);
        var cardEnd = xaml.IndexOf("00 PERFIL (fin)", StringComparison.Ordinal);
        Assert.True(cardStart >= 0 && cardEnd > cardStart);
        var card = xaml.Substring(cardStart, cardEnd - cardStart);
        Assert.Contains("ProfileKnobs", card, StringComparison.Ordinal);
        Assert.DoesNotContain("Readings", card, StringComparison.Ordinal);
        Assert.DoesNotContain("CpuKnobs", card, StringComparison.Ordinal);
        Assert.DoesNotContain("GpuKnobs", card, StringComparison.Ordinal);

        var telemetry = xaml.IndexOf("ViewModel.TelemetryHeader", StringComparison.Ordinal);
        Assert.True(cardEnd < telemetry);
        var tail = xaml.Substring(telemetry);
        Assert.Contains("ViewModel.GpuReadings", tail, StringComparison.Ordinal);
        Assert.Contains("ViewModel.MemoryReadings", tail, StringComparison.Ordinal);
    }

    private static (OverclockViewModel ViewModel, TuningStores Stores) BuildAuto(bool writable)
    {
        var backend = new TuningBackend(writable);
        var stores = new TuningStores();
        var vm = OverclockTestHarness.Create(
            new OverclockReadOnlyRegistry(backend),
            [backend],
            probe: new ScriptedProbe(),
            profileStore: stores.ProfileStore,
            checkpointStore: stores.CheckpointStore,
            journal: stores.Journal);
        return (vm, stores);
    }

    private sealed class TuningStores
    {
        public List<ValidatedOverclockProfile> Profiles { get; } = [];

        public RecordingJournal Journal { get; } = new();

        public IValidatedProfileStore ProfileStore => new SinkProfileStore(Profiles);

        public ITuningCheckpointStore CheckpointStore { get; } = new SinkCheckpointStore();

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
    }

    private sealed class RecordingJournal : IOverclockJournal
    {
        public List<TransactionPhase> Phases { get; } = [];

        public List<string?> Details { get; } = [];

        public ValueTask WriteAsync(TransactionPhase phase, string? detail, CancellationToken cancellationToken)
        {
            Phases.Add(phase);
            Details.Add(detail);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScriptedProbe : IStabilityProbe
    {
        public string Id => "scripted-tuning-probe";

        public IReadOnlyList<ProbeMetric> SupportedMetrics => [ProbeMetric.EffectiveClockStall];

        public Task<ProbeRun> RunAsync(ProbeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ProbeRun(
                Completed: true,
                Aborted: false,
                [new ProbeSample(DateTimeOffset.UnixEpoch, [])],
                null));
    }

    private sealed class AntiCheatGameSource : IGameOverclockSource
    {
        public ValueTask<GameOverclockSnapshot> ReadAsync(CancellationToken ct) =>
            new(new GameOverclockSnapshot(true, ["EAC"], []));
    }

    private sealed class TuningBackend(bool writable) : IOverclockBackend, IOverclockDetailSource, IOverclockWriteGate
    {
        private double _current = 100d;

        public string Id => OverclockCatalog.AmdAdlxBackendId;

        public OverclockCapabilities Probe(CancellationToken ct) => new(
            CpuBackendKind.None,
            GpuBackendKind.AmdAdlx,
            MemoryBackendKind.None,
            [new BackendAvailability(Id, true, string.Empty, null, false, null, EvidenceLevel.Vendor)],
            OverclockCatalog.AmdGpuKnobs);

        public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct) =>
            ValueTask.FromResult(
                id == AmdKnobs.PowerLimit
                    ? KnobRead.Ok(KnobValue.FromScalar(_current))
                    : KnobRead.Fail("overclock-fake-read-disabled"));

        public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct)
        {
            if (!writable || id != AmdKnobs.PowerLimit)
            {
                return ValueTask.FromResult(KnobWriteResult.Blocked("overclock-fake-write-blocked"));
            }

            _current = value.Scalar;
            return ValueTask.FromResult(KnobWriteResult.Ok(value));
        }

        public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) =>
            ValueTask.FromResult(new UnitSnapshot(
                OverclockComponent.Gpu,
                [new KeyValuePair<KnobId, KnobValue>(AmdKnobs.PowerLimit, KnobValue.FromScalar(_current))],
                DateTimeOffset.UnixEpoch,
                null));

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) =>
            ValueTask.FromResult(new RestoreOutcome(true, snapshot.Count, true, null));

        public ValueTask<IReadOnlyList<OverclockReading>> ReadDetailsAsync(CancellationToken ct) =>
            new ValueTask<IReadOnlyList<OverclockReading>>([]);

        public bool CanWrite(KnobId knob) => writable && knob == AmdKnobs.PowerLimit;

        public string? BlockReasonKey(KnobId knob) =>
            CanWrite(knob) ? null : "overclock-fake-write-blocked";
    }

    /// <summary>
    /// La primera lectura (sondeo) responde al instante para que el Refresh deje
    /// filas accionables; solo las siguientes bloquean hasta <see cref="Release"/>
    /// o cancelación. Así el test de cancelación entra deterministamente dentro del
    /// auto en vez de colgar el sondeo 30 s hasta el timeout por fila.
    /// </summary>
    private sealed class BlockingTuningBackend : IOverclockBackend, IOverclockWriteGate
    {
        private double _current = 100d;
        private int _reads;

        public readonly ManualResetEventSlim Entered = new(false);

        public volatile bool Release;

        public string Id => OverclockCatalog.AmdAdlxBackendId;

        public OverclockCapabilities Probe(CancellationToken ct) => new(
            CpuBackendKind.None,
            GpuBackendKind.AmdAdlx,
            MemoryBackendKind.None,
            [new BackendAvailability(Id, true, string.Empty, null, false, null, EvidenceLevel.Vendor)],
            OverclockCatalog.AmdGpuKnobs);

        public async ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _reads) > 1)
            {
                Entered.Set();
                while (!Release)
                {
                    await Task.Delay(10, ct).ConfigureAwait(false);
                }
            }

            return id == AmdKnobs.PowerLimit
                ? KnobRead.Ok(KnobValue.FromScalar(_current))
                : KnobRead.Fail("overclock-fake-read-disabled");
        }

        public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct)
        {
            if (id != AmdKnobs.PowerLimit)
            {
                return ValueTask.FromResult(KnobWriteResult.Blocked("overclock-fake-write-blocked"));
            }

            _current = value.Scalar;
            return ValueTask.FromResult(KnobWriteResult.Ok(value));
        }

        public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) =>
            ValueTask.FromResult(new UnitSnapshot(
                OverclockComponent.Gpu,
                [new KeyValuePair<KnobId, KnobValue>(AmdKnobs.PowerLimit, KnobValue.FromScalar(_current))],
                DateTimeOffset.UnixEpoch,
                null));

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) =>
            ValueTask.FromResult(new RestoreOutcome(true, snapshot.Count, true, null));

        public bool CanWrite(KnobId knob) => knob == AmdKnobs.PowerLimit;

        public string? BlockReasonKey(KnobId knob) =>
            CanWrite(knob) ? null : "overclock-fake-write-blocked";
    }

    /// <summary>CPU deliberadamente no escribible: da filas no accionables al test de la tarjeta.</summary>
    private sealed class CpuBlockedBackend : IOverclockBackend, IOverclockWriteGate
    {
        private readonly Dictionary<string, KnobValue> _current = new(StringComparer.Ordinal);

        public CpuBlockedBackend()
        {
            foreach (var knob in OverclockCatalog.CpuPolicyKnobs)
            {
                _current[knob.Id.Value] = KnobValue.FromScalar(80d);
            }
        }

        public string Id => OverclockCatalog.IntelPolicyBackendId;

        public OverclockCapabilities Probe(CancellationToken ct) => new(
            CpuBackendKind.PolicyOnly,
            GpuBackendKind.None,
            MemoryBackendKind.None,
            [new BackendAvailability(Id, true, string.Empty, null, false, "fake", EvidenceLevel.Vendor)],
            OverclockCatalog.CpuPolicyKnobs);

        public bool CanWrite(KnobId knob) => false;

        public string? BlockReasonKey(KnobId knob) => "overclock-fake-write-blocked";

        public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct) =>
            ValueTask.FromResult(
                _current.TryGetValue(id.Value, out var value)
                    ? KnobRead.Ok(value)
                    : KnobRead.Fail("overclock-fake-read-disabled"));

        public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct) =>
            ValueTask.FromResult(KnobWriteResult.Blocked("overclock-fake-write-blocked"));

        public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) =>
            ValueTask.FromResult(new UnitSnapshot(
                OverclockComponent.Cpu,
                _current.Select(kv => new KeyValuePair<KnobId, KnobValue>(new KnobId(kv.Key), kv.Value)).ToList(),
                DateTimeOffset.UnixEpoch,
                "fake"));

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) =>
            ValueTask.FromResult(new RestoreOutcome(true, snapshot.Count, true, null));
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CA-O.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("CA-O.sln not found above the test host.");
        }

        return File.ReadAllText(Path.Combine([directory.FullName, .. parts]));
    }
}
