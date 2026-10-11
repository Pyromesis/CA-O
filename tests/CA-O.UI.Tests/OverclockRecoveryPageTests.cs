using CAO.Core.Overclock;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Reporting;
using CAO.Core.Overclock.Stability;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Overclock;
using CAO.Infrastructure.Overclock.Reporting;
using CAO.UI.ViewModels;
using CAO.Shared;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// The recovery block on the Overclock page: the three decisions the design
/// promises after a restart, and the real ceiling each setting runs into.
///
/// The engine is the production one. Only the store, the boot probe and the
/// target are substituted, because those are the parts that would otherwise
/// need a machine that actually restarted mid-tuning.
/// </summary>
public sealed class OverclockRecoveryPageTests
{
    private static readonly Guid RunId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly DateTimeOffset BootUtc = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PendingRuns_AreListedWithTheKnobAndTheReason()
    {
        var (vm, _, store) = Build();
        await SeedAsync(store);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Single(vm.PendingRecoveries);
        Assert.Equal("test.gpu.offset", vm.PendingRecoveries[0].Knob);
        Assert.Equal(RunId.ToString(), vm.PendingRecoveries[0].RunId);

        // The row must say why it is being offered; a bare knob with no reason
        // is a control the user cannot judge.
        Assert.NotEqual(string.Empty, vm.PendingRecoveries[0].Reason);
        Assert.NotEqual(RecoveryPolicy.ReasonFirstUnclean, vm.PendingRecoveries[0].Reason);
        Assert.NotEmpty(vm.RecoverySummary);
    }

    [Fact]
    public async Task NoPendingRuns_SaysSoInsteadOfLeavingAnEmptyBlock()
    {
        var (vm, _, store) = Build();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.PendingRecoveries);
        Assert.NotEqual(string.Empty, vm.RecoverySummary);
    }

    [Fact]
    public async Task EveryPendingRow_CarriesTheThreeDecisionCommands()
    {
        var (vm, _, store) = Build();
        await SeedAsync(store);

        await vm.RefreshCommand.ExecuteAsync(null);

        var row = Assert.Single(vm.PendingRecoveries);
        Assert.NotNull(row.RevertCommand);
        Assert.NotNull(row.KeepCommand);
        Assert.NotNull(row.DiscardCommand);
    }

    [Fact]
    public async Task Revert_WritesBackTheLastKnownGoodValue_AndClosesTheRun()
    {
        var target = new StubTarget();
        var (vm, _, store) = Build(target);
        await SeedAsync(store);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = Assert.Single(vm.PendingRecoveries);

        await vm.RevertRecoveryCommand.ExecuteAsync(row);

        Assert.Equal("10", OverclockTestNumbers.Describe(target.Peek("test.gpu.offset")!.Value));
        Assert.NotEqual(string.Empty, vm.StatusLine);
        Assert.Empty(vm.PendingRecoveries);
    }

    [Fact]
    public async Task Keep_LeavesTheHardwareAlone_AndClosesTheRun()
    {
        var target = new StubTarget();
        var (vm, _, store) = Build(target);
        await SeedAsync(store);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = Assert.Single(vm.PendingRecoveries);

        await vm.KeepRecoveryCommand.ExecuteAsync(row);

        Assert.Empty(target.AppliedWrites);
        Assert.Empty(vm.PendingRecoveries);
    }

    [Fact]
    public async Task Discard_ForgetsTheRunWithoutTouchingTheHardware()
    {
        var target = new StubTarget();
        var (vm, _, store) = Build(target);
        await SeedAsync(store);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = Assert.Single(vm.PendingRecoveries);

        await vm.DiscardRecoveryCommand.ExecuteAsync(row);

        Assert.Empty(target.AppliedWrites);
        Assert.Empty(vm.PendingRecoveries);
    }

    [Fact]
    public async Task ARefusedRevert_KeepsTheRunVisible_AndSaysItFailed()
    {
        var refusing = new StubTarget { RefuseRevert = true };
        var (vm, _, store) = Build(new StubTarget { RefuseRevert = true });
        await SeedAsync(store);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = Assert.Single(vm.PendingRecoveries);

        await vm.RevertRecoveryCommand.ExecuteAsync(row);

        Assert.NotEqual(string.Empty, vm.StatusLine);

        // The machine is still on the value that crashed it and the run is
        // still open, so the row has to keep being offered.
        Assert.Single(vm.PendingRecoveries);
    }

    [Fact]
    public async Task AFailureScanningTheRuns_DoesNotTakeTheHardwareViewDown()
    {
        var (vm, _, store) = Build(throwing: true);

        await vm.RefreshCommand.ExecuteAsync(null);

        // The knobs of an empty registry are legitimately empty; what matters is
        // that the scan reported its own failure instead of throwing.
        Assert.NotNull(vm.StatusLine);
        Assert.NotEqual(string.Empty, vm.RecoverySummary);
    }

    [Fact]
    public async Task Ceilings_AreShownWithTheirRealRange()
    {
        var ceilings = new StubCeilingSource(
        [
            new TuningCeiling(NvKnobs.CoreClockOffset, "overclock-unit-hertz", -500_000_000d, 500_000_000d, null),
        ]);
        var (vm, _, store) = Build(ceilings: ceilings);

        await vm.RefreshCommand.ExecuteAsync(null);

        var row = Assert.Single(vm.Ceilings);
        Assert.Equal(NvKnobs.CoreClockOffset, row.Knob);
        Assert.Contains("-500", row.Range, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, row.Reason);
    }

    [Fact]
    public async Task ACelingWithoutNumbers_KeepsItsRowAndShowsTheReason()
    {
        var ceilings = new StubCeilingSource(
        [
            new TuningCeiling(
                NvKnobs.PowerLimit,
                "overclock-unit-percent",
                null,
                null,
                "overclock-ceiling-unavailable"),
        ]);
        var (vm, _, store) = Build(ceilings: ceilings);

        await vm.RefreshCommand.ExecuteAsync(null);

        var row = Assert.Single(vm.Ceilings);
        Assert.Equal(string.Empty, row.Range);
        Assert.NotEqual(string.Empty, row.Reason);
    }

    [Fact]
    public void ThePage_WiresEveryRecoverySurface()
    {
        var xaml = ReadRepoFile("src", "CA-O.UI", "OverclockPage.xaml");

        Assert.Contains("ViewModel.PendingRecoveries", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.Ceilings", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.RecoverySummary", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Bind RevertCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Bind KeepCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Bind DiscardCommand", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("overclock-unit-percent")]
    [InlineData("overclock-ceiling-range")]
    [InlineData("overclock-recovery-pending")]
    [InlineData("overclock-recovery-done")]
    [InlineData("overclock-recovery-revert")]
    [InlineData("overclock-boot-info-unavailable")]
    [InlineData("overclock-ceiling-enforced-below-max")]
    [InlineData("recovery-user-revert-failed")]
    [InlineData("recovery-already-decided-this-boot")]
    public void EveryRecoveryKey_IsTranslatedInBothLanguages(string key)
    {
        var localizer = ReadRepoFile("src", "CA-O.UI", "Resources", "Localizer.cs");
        Assert.Equal(2, CountOccurrences(localizer, $"[\"{key}\"]"));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static async Task SeedAsync(StubCheckpointStore store)
    {
        // A run that was updated before this boot: the restart is what the page
        // is being asked about.
        var run = new TuningCheckpoint
        {
            RunId = RunId,
            BackendId = "fake-backend",
            Knob = "test.gpu.offset",
            Component = "Gpu",
            AppliedValue = 30,
            LastKnownGoodValue = 10,
            Phase = CheckpointPhase.StepApplied,
            UpdatedAtUtc = BootUtc.AddHours(-1),
            DriverVersion = "999.99",
            ConsecutiveUncleanBoots = 0,
        };

        await store.SeedAsync(run, CancellationToken.None);
    }

    private static (OverclockViewModel ViewModel, OverclockRecoveryService Service, StubCheckpointStore Store) Build(
        StubTarget? target = null,
        bool throwing = false,
        IOverclockCeilingSource? ceilings = null)
    {
        target ??= new StubTarget();
        var store = new StubCheckpointStore();
        var service = new OverclockRecoveryService(
            store,
            throwing ? new ThrowingBootProbe() : new StubBootProbe(),
            new StubResolver(target));

        var vm = new OverclockViewModel(
            new OverclockReadOnlyRegistry(),
            [],
            new GameDetectionProvider(),
            recovery: service,
            ceilingSource: ceilings ?? new StubCeilingSource([]),
            detectGames: _ => Task.FromResult<IReadOnlyList<DetectedGame>>([]));

        return (vm, service, store);
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CA-O.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("CA-O.sln not found above the test output directory.");
        }

        return File.ReadAllText(Path.Combine([dir.FullName, .. parts]));
    }

    private static class OverclockTestNumbers
    {
        public static string Describe(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class StubTarget : ITuningTarget
    {
        public string BackendId => "fake-backend";

        public bool RefuseRevert { get; init; }

        public List<string> AppliedWrites { get; } = [];

        private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);

        public double? Peek(string knobId) => _values.TryGetValue(knobId, out var v) ? v : null;

        public ValueTask<UnitSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new UnitSnapshot(OverclockComponent.Gpu, [], BootUtc, "999.99"));

        public ValueTask<KnobWriteResult> ApplyAsync(KnobId id, KnobValue value, CancellationToken cancellationToken) =>
            Write(id, value, cancellationToken);

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new RestoreOutcome(true, snapshot.Count, true, null));

        public ValueTask<KnobWriteResult> RestoreValueAsync(KnobId id, KnobValue value, CancellationToken cancellationToken) =>
            RefuseRevert
                ? ValueTask.FromResult(KnobWriteResult.Blocked("overclock-gpu-driver-blocked"))
                : Write(id, value, cancellationToken);

        private ValueTask<KnobWriteResult> Write(KnobId id, KnobValue value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values[id.Value] = value.Scalar;
            AppliedWrites.Add(OverclockTestNumbers.Describe(value.Scalar));
            return ValueTask.FromResult(KnobWriteResult.Ok(value));
        }
    }

    private sealed class StubResolver(StubTarget target) : ITuningTargetResolver
    {
        public bool TryResolve(string backendId, out ITuningTarget? resolved)
        {
            resolved = backendId == target.BackendId ? target : null;
            return resolved is not null;
        }
    }

    private sealed class StubBootProbe : IBootHealthProbe
    {
        public Task<BootHealthReport> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new BootHealthReport(false, BootUtc, "unclean"));
    }

    private sealed class ThrowingBootProbe : IBootHealthProbe
    {
        public Task<BootHealthReport> GetAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("probe exploded");
    }

    private sealed class StubCheckpointStore : ITuningCheckpointStore
    {
        private readonly Dictionary<Guid, TuningCheckpoint> _items = [];

        public Task SeedAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            _items[checkpoint.RunId] = checkpoint;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TuningCheckpoint>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TuningCheckpoint>>(_items.Values.ToList());

        public Task SaveAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            _items[checkpoint.RunId] = checkpoint;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.Remove(runId));

        public Task<string?> QuarantineAsync(Guid runId, string reasonKey, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubCeilingSource(IReadOnlyList<TuningCeiling> ceilings) : IOverclockCeilingSource
    {
        public ValueTask<IReadOnlyList<TuningCeiling>> ReadAsync(CancellationToken ct) =>
            new(ceilings);
    }
}