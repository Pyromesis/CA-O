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
/// The guided tuning flow: actionable knobs offer a tune target, a stable run
/// saves a validated profile, a refused write saves nothing. Fakes are injected
/// into the real ViewModel, engine included.
/// </summary>
public sealed class OverclockTuningPageTests
{
    [Fact]
    public async Task ActionableKnobs_OfferATuneTarget()
    {
        var (vm, _) = Build(writable: true);

        await vm.RefreshCommand.ExecuteAsync(null);

        var target = Assert.Single(vm.TuningTargets);
        Assert.Equal("amd.gpu.powerLimit", target.KnobId);
        Assert.NotNull(target.TuneCommand);
    }

    [Fact]
    public async Task TuningRun_SavesAValidatedProfile_WhenEveryPointPasses()
    {
        var (vm, stores) = Build(writable: true);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.StartTuningCommand.ExecuteAsync("amd.gpu.powerLimit");

        var profile = Assert.Single(stores.Profiles);
        Assert.Equal(new KnobId("amd.gpu.powerLimit"), profile.Knob);
        Assert.Equal(120d, profile.StableValue);
        Assert.Equal(120d, profile.StableCeiling);
        Assert.Equal(21, profile.PointsPassed);
        Assert.NotEqual("overclock-tuning-validated", vm.StatusLine);
        Assert.Contains(stores.Journal.Phases, p => p == TransactionPhase.OverclockStarted);
        Assert.Contains(stores.Journal.Phases, p => p == TransactionPhase.OverclockStepApplied);
    }

    [Fact]
    public async Task RejectedWrite_SavesNoProfile_AndSaysSo()
    {
        var (vm, stores) = Build(writable: false);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.StartTuningCommand.ExecuteAsync("amd.gpu.powerLimit");

        Assert.Empty(stores.Profiles);
        Assert.NotEqual("overclock-tuning-rejected", vm.StatusLine);
    }

    [Fact]
    public async Task UnknownKnob_DoesNotStartARun()
    {
        var (vm, stores) = Build(writable: true);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.StartTuningCommand.ExecuteAsync("no.such.knob");

        Assert.Empty(stores.Journal.Phases);
        Assert.NotEqual("overclock-tuning-unknown", vm.StatusLine);
    }

    [Fact]
    public void ThePage_WiresTheTuningSection()
    {
        var xaml = ReadRepoFile("src", "CA-O.UI", "OverclockPage.xaml");

        Assert.Contains("ViewModel.TuningTargets", xaml);
        Assert.Contains("vm:OverclockTuningRow", xaml);
        Assert.Contains("CommandParameter=\"{x:Bind KnobId}\"", xaml);
    }

    private static (OverclockViewModel ViewModel, TuningStores Stores) Build(bool writable)
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

            public Task<IReadOnlyList<TuningCheckpoint>> ListAsync(CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<TuningCheckpoint>>(Items.Values.ToList());

            public Task SaveAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken)
            {
                Items[checkpoint.RunId] = checkpoint;
                return Task.CompletedTask;
            }

            public Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken) =>
                Task.FromResult(Items.Remove(runId));

            public Task<string?> QuarantineAsync(Guid runId, string reasonKey, CancellationToken cancellationToken) =>
                Task.FromResult<string?>(null);
        }
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
