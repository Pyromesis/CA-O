using CAO.Core.Overclock;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Stability;
using CAO.Core.Tests.OverclockFixtures;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// The three buttons the design promises after a restart (D4), and the promise
/// that a single restart is never counted twice.
///
/// Both matter for the same reason: a wrong answer here either reverts a setting
/// the user did not ask to lose, or leaves a machine running the value that
/// crashed it while the page says everything is fine.
/// </summary>
public sealed class OverclockRecoveryChoiceTests
{
    private static readonly Guid RunId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset BootUtc = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Resolve_RevertsToTheLastKnownGood_WhenTheBackendAcceptsIt()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OpenRun(), CancellationToken.None);

        var target = new FakeOverclockBackend();
        var journal = new RecordingJournal();
        var service = Service(store, new StubTargetResolver().Register("fake-backend", target), journal: journal);

        var result = await service.ResolveAsync(RunId, RecoveryChoice.Revert, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.RunClosed);
        Assert.Equal(RecoveryResolveResult.ReasonUserReverted, result.ReasonKey);
        Assert.Equal(CheckpointPhase.AutoReverted, result.Phase);

        // The machine gets the value it had before the tuning run, not a guess.
        Assert.Equal("10", OverclockTestKnobs.Describe(target.Peek("test.gpu.offset")!.Value));
        Assert.Equal(CheckpointPhase.AutoReverted, store.Get(RunId)!.Phase);
        Assert.Equal(0, store.Get(RunId)!.ConsecutiveUncleanBoots);
        Assert.True(journal.Contains(TransactionPhase.OverclockInterrupted));
    }

    [Fact]
    public async Task Resolve_KeepsTheRunOpen_WhenTheRevertIsRefused()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OpenRun(), CancellationToken.None);

        var refused = new RevertRefusingBackend();
        var service = Service(store, new StubTargetResolver().Register("fake-backend", refused));

        var result = await service.ResolveAsync(RunId, RecoveryChoice.Revert, CancellationToken.None);

        // Closing the run here would hide a machine that is still running the
        // value that crashed it, so a refused revert leaves everything open.
        Assert.False(result.Succeeded);
        Assert.False(result.RunClosed);
        Assert.Equal(RevertRefusingBackend.Reason, result.ReasonKey);
        Assert.Equal(CheckpointPhase.Interrupted, store.Get(RunId)!.Phase);
    }

    [Fact]
    public async Task Resolve_DoesNotRevert_WhenTheBackendIsGone()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OpenRun(), CancellationToken.None);

        var service = Service(store, new StubTargetResolver());

        var result = await service.ResolveAsync(RunId, RecoveryChoice.Revert, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.RunClosed);
        Assert.Equal(OverclockRecoveryService.ReasonBackendUnavailable, result.ReasonKey);

        // The checkpoint is left exactly as it was. Nothing was attempted, so
        // marking it "interrupted" would claim a warning the user never got,
        // and the run stays open so the recovery offers it again.
        Assert.Equal(CheckpointPhase.StepApplied, store.Get(RunId)!.Phase);
    }

    [Fact]
    public async Task Resolve_KeepsTheValueAndClosesTheRun_WhenTheUserChoosesKeep()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OpenRun(), CancellationToken.None);

        var target = new FakeOverclockBackend();
        var service = Service(store, new StubTargetResolver().Register("fake-backend", target));

        var result = await service.ResolveAsync(RunId, RecoveryChoice.Keep, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.RunClosed);
        Assert.Equal(RecoveryResolveResult.ReasonUserKept, result.ReasonKey);
        Assert.Equal(CheckpointPhase.Closed, store.Get(RunId)!.Phase);

        // "Keep" means keep: nothing is written back to the hardware.
        Assert.Empty(target.AppliedWrites);
    }

    [Fact]
    public async Task Resolve_ForgetsTheCheckpoint_WhenTheUserChoosesDiscard()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OpenRun(), CancellationToken.None);

        var service = Service(store, new StubTargetResolver().Register("fake-backend", new FakeOverclockBackend()));

        var result = await service.ResolveAsync(RunId, RecoveryChoice.Discard, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.RunClosed);
        Assert.Equal(RecoveryResolveResult.ReasonUserDiscarded, result.ReasonKey);
        Assert.Null(store.Get(RunId));
        Assert.Contains(RunId, store.Deleted);
    }

    [Fact]
    public async Task Resolve_KeepsTheRunOpen_WhenTheCheckpointCannotBeDeleted()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OpenRun(), CancellationToken.None);

        // A store that refuses the delete is the honest case: the run is still
        // on disk, so the recovery has to offer it again next time.
        var stubborn = new UndeletableStore(store);
        var service = Service(stubborn, new StubTargetResolver().Register("fake-backend", new FakeOverclockBackend()));

        var result = await service.ResolveAsync(RunId, RecoveryChoice.Discard, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.RunClosed);
        Assert.Equal(RecoveryResolveResult.ReasonDiscardFailed, result.ReasonKey);
        Assert.NotNull(store.Get(RunId));
    }

    [Fact]
    public async Task Resolve_ReportsNotFound_WhenThereIsNoSuchRun()
    {
        var service = Service(new InMemoryCheckpointStore(), new StubTargetResolver());

        var result = await service.ResolveAsync(RunId, RecoveryChoice.Revert, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.RunClosed);
        Assert.Equal(RecoveryResolveResult.ReasonRunNotFound, result.ReasonKey);
    }

    [Fact]
    public async Task Resolve_ChangesNothing_WhenTheRunIsAlreadyTerminal()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(
            OpenRun() with { Phase = CheckpointPhase.Closed },
            CancellationToken.None);

        var target = new FakeOverclockBackend();
        var service = Service(store, new StubTargetResolver().Register("fake-backend", target));

        var result = await service.ResolveAsync(RunId, RecoveryChoice.Revert, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(result.RunClosed);
        Assert.Equal(RecoveryResolveResult.ReasonAlreadyClosed, result.ReasonKey);
        Assert.Empty(target.AppliedWrites);
    }

    [Fact]
    public async Task Recover_OffersTheRun_OnTheFirstRestartWithoutRevertingIt()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OpenRun(), CancellationToken.None);

        var target = new FakeOverclockBackend();
        var service = Service(
            store,
            new StubTargetResolver().Register("fake-backend", target),
            boot: new StubBootHealth(new BootHealthReport(false, BootUtc, "unclean")));

        var outcomes = await service.RecoverAllAsync(CancellationToken.None);

        Assert.Single(outcomes);
        Assert.True(outcomes[0].NeedsUserDecision);
        Assert.Equal(RecoveryPolicy.ReasonFirstUnclean, outcomes[0].ReasonKey);
        Assert.Equal(CheckpointPhase.Interrupted, store.Get(RunId)!.Phase);
        Assert.Empty(target.AppliedWrites);
    }

    [Fact]
    public async Task Recover_DoesNotCountTheSameRestartTwice_WhenThePageIsOpenedAgain()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OpenRun(), CancellationToken.None);

        var target = new FakeOverclockBackend();
        var resolver = new StubTargetResolver().Register("fake-backend", target);
        var service = Service(
            store,
            resolver,
            boot: new StubBootHealth(new BootHealthReport(false, BootUtc, "unclean")));

        await service.RecoverAllAsync(CancellationToken.None);
        var second = await service.RecoverAllAsync(CancellationToken.None);

        // D4 promises one warning and one revert, not one per visit to the page.
        Assert.Equal(1, store.Get(RunId)!.ConsecutiveUncleanBoots);
        Assert.Empty(target.AppliedWrites);

        Assert.Single(second);
        Assert.True(second[0].NeedsUserDecision);
        Assert.Equal(OverclockRecoveryService.ReasonAlreadyDecided, second[0].ReasonKey);
    }

    [Fact]
    public async Task Recover_TreatsARestartDuringTheRunAsUnclean_EvenWhenTheProbeCallsItClean()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(
            OpenRun() with { UpdatedAtUtc = BootUtc.AddHours(-1) },
            CancellationToken.None);

        var target = new FakeOverclockBackend();
        var service = Service(
            store,
            new StubTargetResolver().Register("fake-backend", target),
            boot: new StubBootHealth(new BootHealthReport(true, BootUtc, null)));

        var outcomes = await service.RecoverAllAsync(CancellationToken.None);

        // The probe cannot see a crash, but it can see that the run is older than
        // the boot, and that is enough to refuse to call it clean.
        Assert.Single(outcomes);
        Assert.True(outcomes[0].NeedsUserDecision);
        Assert.Equal(RecoveryPolicy.ReasonFirstUnclean, outcomes[0].ReasonKey);
    }

    [Fact]
    public async Task Recover_RevertsAutomatically_OnTheSecondRestartInARow()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OpenRun(), CancellationToken.None);

        var target = new FakeOverclockBackend();
        var resolver = new StubTargetResolver().Register("fake-backend", target);

        var firstBoot = Service(
            store,
            resolver,
            boot: new StubBootHealth(new BootHealthReport(false, BootUtc.AddHours(-3), "unclean")));
        await firstBoot.RecoverAllAsync(CancellationToken.None);

        // A different boot: the marker no longer matches, so this restart counts.
        var secondBoot = Service(
            store,
            resolver,
            boot: new StubBootHealth(new BootHealthReport(false, BootUtc, "unclean")));
        var outcomes = await secondBoot.RecoverAllAsync(CancellationToken.None);

        Assert.Single(outcomes);
        Assert.Equal(RecoveryAction.AutoRevert, outcomes[0].Action);
        Assert.True(outcomes[0].RevertSucceeded);
        Assert.Equal(CheckpointPhase.AutoReverted, store.Get(RunId)!.Phase);
        Assert.Equal("10", OverclockTestKnobs.Describe(target.Peek("test.gpu.offset")!.Value));
    }

    private static OverclockRecoveryService Service(
        ITuningCheckpointStore store,
        ITuningTargetResolver resolver,
        IBootHealthProbe? boot = null,
        IOverclockJournal? journal = null) =>
        new(
            store,
            boot ?? new StubBootHealth(BootHealthReport.Clean),
            resolver,
            journal);

    private static TuningCheckpoint OpenRun() =>
        OverclockTestKnobs.Checkpoint(
            RunId,
            CheckpointPhase.StepApplied,
            applied: 30,
            lastKnownGood: 10,
            consecutiveUncleanBoots: 0,
            driverVersion: "999.99",
            knob: "test.gpu.offset",
            backendId: "fake-backend");

    /// <summary>
    /// A target whose hardware accepts the revert in every direction except the
    /// one that matters: going back to the last known good value.
    /// </summary>
    private sealed class RevertRefusingBackend() : FakeOverclockBackend
    {
        public const string Reason = "overclock-gpu-driver-blocked";

        public override ValueTask<KnobWriteResult> RestoreValueAsync(
            KnobId id,
            KnobValue value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(KnobWriteResult.Blocked(Reason));
        }
    }
    /// <summary>Wraps a store and refuses to delete, which is the honest failure case.</summary>
    private sealed class UndeletableStore(InMemoryCheckpointStore inner) : ITuningCheckpointStore
    {
        public Task<IReadOnlyList<TuningCheckpoint>> ListAsync(CancellationToken cancellationToken) =>
            inner.ListAsync(cancellationToken);

        public Task SaveAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken) =>
            inner.SaveAsync(checkpoint, cancellationToken);

        public Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<string?> QuarantineAsync(Guid runId, string reasonKey, CancellationToken cancellationToken) =>
            inner.QuarantineAsync(runId, reasonKey, cancellationToken);
    }
}