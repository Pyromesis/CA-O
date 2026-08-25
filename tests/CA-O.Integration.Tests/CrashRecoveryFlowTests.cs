using CAO.Core.Abstractions;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Integration.Tests;

/// <summary>
/// Crash-recovery flow (spec 13/100): a snapshot without a committed+verified
/// history entry is Incomplete; the system reconstructs what happened and
/// closes the loop only after a real revert.
/// </summary>
public sealed class CrashRecoveryFlowTests
{
    private sealed class MemoryStore : ISnapshotStore
    {
        public Dictionary<string, OptimizationSnapshot> Saved { get; } = new(StringComparer.Ordinal);

        public IEnumerable<string> ListIds() => Saved.Keys;

        public void Save(string optimizationId, OptimizationSnapshot snapshot) => Saved[optimizationId] = snapshot;

        public bool TryLoad(string optimizationId, out OptimizationSnapshot snapshot)
        {
            if (Saved.TryGetValue(optimizationId, out var found))
            {
                snapshot = found;
                return true;
            }
            snapshot = new OptimizationSnapshot();
            return false;
        }

        public void Delete(string optimizationId) => Saved.Remove(optimizationId);
    }

    private sealed class MemoryHistory : IHistoryLogger
    {
        public List<HistoryEntry> Entries { get; } = [];

        public void Log(HistoryEntry entry) => Entries.Add(entry);

        public IReadOnlyList<HistoryEntry> ReadLast(int maxEntries) => Entries.TakeLast(maxEntries).ToList();
    }

    private readonly MemoryStore _snapshots = new();
    private readonly MemoryHistory _history = new();

    [Fact]
    public void SnapshotWithoutCommittedApplyIsIncomplete()
    {
        // Simulate: transaction persisted snapshot, then the process died.
        _snapshots.Save("disable-transparency",
            new OptimizationSnapshot { TimestampUtc = DateTime.UtcNow.AddMinutes(-5) });

        var service = new CrashRecoveryService(_snapshots, _history, _ => OptimizationState.Unknown);

        var candidate = Assert.Single(service.Scan());
        Assert.Equal("disable-transparency", candidate.OptimizationId);
    }

    [Fact]
    public void CommittedAndVerifiedChangeIsNotARecoveryCandidate()
    {
        _snapshots.Save("disable-transparency", new OptimizationSnapshot());
        _history.Log(new HistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            OptimizationId = "disable-transparency",
            Operation = "apply",
            Success = true,
            ApplyResult = "success",
            Verification = "passed",
        });

        var service = new CrashRecoveryService(_snapshots, _history, _ => OptimizationState.AppliedByCao);

        Assert.Empty(service.Scan());
    }

    [Fact]
    public void FailedApplyBecomesCandidateCarryingLiveState()
    {
        _snapshots.Save("maximum-power-plan", new OptimizationSnapshot());
        _history.Log(new HistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            OptimizationId = "maximum-power-plan",
            Operation = "apply",
            Success = false,
            ApplyResult = "failed",
        });

        var service = new CrashRecoveryService(_snapshots, _history, _ => OptimizationState.NotApplied);

        var candidate = Assert.Single(service.Scan());
        Assert.Equal(OptimizationState.NotApplied, candidate.LiveState);
    }

    [Fact]
    public void MarkRecoveredClosesSnapshotOnlyAfterSuccessfulRevert()
    {
        _snapshots.Save("disable-vbs", new OptimizationSnapshot());
        var service = new CrashRecoveryService(_snapshots, _history, _ => OptimizationState.NotApplied);

        service.MarkRecovered("disable-vbs", revertSucceeded: true);

        Assert.DoesNotContain("disable-vbs", _snapshots.ListIds());
        Assert.Contains(_history.Entries, entry => entry.Operation == "recovery" && entry.Success);

        // A failed revert keeps the snapshot so the user can retry.
        service.MarkRecovered("disable-vbs", revertSucceeded: false);
        Assert.Contains(_history.Entries, entry => entry.Operation == "recovery" && !entry.Success);
    }
}
