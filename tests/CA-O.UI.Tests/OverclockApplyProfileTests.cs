using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Stability;
using CAO.Infrastructure.Overclock;
using CAO.Shared;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// Aplicar un perfil del catalogo: cada knob accionable recibe su preset en
/// una sola pasada, los bloqueados se omiten y se cuentan, los snapshots
/// permiten restaurar desde la pagina, y el journal solo lleva fases no
/// terminales. Fakes inyectados en el ViewModel real via harness.
/// </summary>
public sealed class OverclockApplyProfileTests
{
    [Fact]
    public async Task ApplyProfile_WritesBalancedPresets_ToEveryActionableKnob()
    {
        var (vm, backend, journal, checkpoints) = Build(writable: true);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(OverclockProfile.Balanced, vm.SelectedChoice?.Profile);
        Assert.NotEmpty(vm.CpuKnobs);
        Assert.All(vm.CpuKnobs, r => Assert.True(r.IsActionable));

        await vm.ApplyProfileCommand.ExecuteAsync(null);

        Assert.Equal(OverclockCatalog.CpuPolicyKnobs.Count, backend.Writes.Count);
        foreach (var knob in OverclockCatalog.CpuPolicyKnobs)
        {
            var expected = Assert.NotNull(knob.PresetValue(OverclockProfile.Balanced));
            var actual = Assert.Single(backend.Writes, w => w.Id == knob.Id);
            Assert.Equal(expected.Scalar, actual.Value.Scalar);
            Assert.Equal(expected.Index, actual.Value.Index);
        }

        Assert.Contains(journal.Phases, p => p == TransactionPhase.OverclockStarted);
        Assert.Equal(
            OverclockCatalog.CpuPolicyKnobs.Count,
            journal.Phases.Count(p => p == TransactionPhase.OverclockStepApplied));
        Assert.Equal(OverclockCatalog.CpuPolicyKnobs.Count, checkpoints.Items.Count);
        Assert.All(checkpoints.Items.Values, c => Assert.Equal(CheckpointPhase.Closed, c.Phase));
        Assert.NotEqual("overclock-profile-summary", vm.ApplySummary);
        Assert.Contains(
            OverclockCatalog.CpuPolicyKnobs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            vm.ApplySummary);
    }

    [Fact]
    public async Task ApplyProfile_WithBlockedBackend_WritesNothing_AndSaysSo()
    {
        var (vm, backend, journal, _) = Build(writable: false);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.CpuKnobs);
        Assert.All(vm.CpuKnobs, r => Assert.False(r.IsActionable));

        await vm.ApplyProfileCommand.ExecuteAsync(null);

        Assert.Empty(backend.Writes);
        Assert.Empty(journal.Phases);
        Assert.NotEqual("overclock-profile-no-actionable", vm.ApplySummary);
    }

    [Fact]
    public async Task ApplyProfile_SavesSnapshots_AndRestore_PutsThemBack()
    {
        var (vm, backend, journal, _) = Build(writable: true);

        var before = new Dictionary<string, KnobValue>();
        foreach (var knob in OverclockCatalog.CpuPolicyKnobs)
        {
            before[knob.Id.Value] = backend.Peek(knob.Id.Value);
        }

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.ApplyProfileCommand.ExecuteAsync(null);

        Assert.Equal(1, journal.Phases.Count(p => p == TransactionPhase.OverclockStarted));

        await vm.RestoreAppliedCommand.ExecuteAsync(null);

        Assert.Equal(2, journal.Phases.Count(p => p == TransactionPhase.OverclockStarted));

        foreach (var knob in OverclockCatalog.CpuPolicyKnobs)
        {
            Assert.Equal(before[knob.Id.Value], backend.Peek(knob.Id.Value));
        }

        Assert.NotEqual("overclock-profile-restored", vm.ApplySummary);
    }

    [Fact]
    public async Task ApplyProfile_JournalsOnlyNonTerminalPhases()
    {
        var (vm, _, journal, _) = Build(writable: true);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.ApplyProfileCommand.ExecuteAsync(null);

        // Terminales explicitas a proposito: si alguien cierra la transaccion
        // desde el apply, este test cae. La lista vive aqui (no en una API) para
        // no acoplar el test al contrato que protege.
        var terminal = new[]
        {
            TransactionPhase.Commit,
            TransactionPhase.RolledBack,
            TransactionPhase.Failed,
            TransactionPhase.CancelledBeforeApply,
            TransactionPhase.RecoveryCompleted,
        };
        Assert.All(journal.Phases, p => Assert.DoesNotContain(p, terminal));
        Assert.Contains(journal.Phases, p => p == TransactionPhase.OverclockStarted);
    }

    [Fact]
    public void ThePage_WiresTheProfileCard()
    {
        var xaml = ReadRepoFile("src", "CA-O.UI", "OverclockPage.xaml");

        Assert.Contains("00  PERFIL", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.ProfileChoices", xaml, StringComparison.Ordinal);
        Assert.Contains("vm:OverclockProfileChoice", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.ApplyProfileCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.RestoreAppliedCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.ApplySummary", xaml, StringComparison.Ordinal);
    }

    private static (OverclockViewModel ViewModel, ApplyBackend Backend, RecordingJournal Journal, SinkCheckpointStore Checkpoints) Build(bool writable)
    {
        var backend = new ApplyBackend(writable);
        var journal = new RecordingJournal();
        var checkpoints = new SinkCheckpointStore();
        var vm = OverclockTestHarness.Create(
            new OverclockReadOnlyRegistry(backend),
            [backend],
            checkpointStore: checkpoints,
            journal: journal);
        return (vm, backend, journal, checkpoints);
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

    private sealed class ApplyBackend : IOverclockBackend, IOverclockWriteGate
    {
        private readonly bool _writable;
        private readonly Dictionary<string, KnobValue> _current = new(StringComparer.Ordinal);

        public ApplyBackend(bool writable)
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

        public KnobValue Peek(string knobId) => _current[knobId];

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
