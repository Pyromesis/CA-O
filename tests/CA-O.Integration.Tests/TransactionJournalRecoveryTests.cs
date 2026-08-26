using CAO.Core.Abstractions;
using CAO.Core.Rollback;
using CAO.Infrastructure.Persistence;
using CAO.Shared;
using Xunit;

namespace CAO.Integration.Tests;

/// <summary>
/// Crash recovery driven by the transaction journal (P0-4): decisions come
/// from journal + snapshot + live state, never from history-by-optimization.
/// </summary>
public sealed class TransactionJournalRecoveryTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "cao-txn-" + Guid.NewGuid().ToString("N"));

    private FileTransactionJournal NewJournal() => new(Path.Combine(_dir, Guid.NewGuid().ToString("N")));

    private static MemoryTxSnapshotStore NewStore() => new();

    private sealed class MemoryTxSnapshotStore : ISnapshotStore
    {
        public Dictionary<Guid, TransactionSnapshotRecord> Saved { get; } = new();

        public void Save(TransactionSnapshotRecord record) =>
            Saved[record.Manifest.TransactionId] = record;

        public bool TryLoad(Guid transactionId, out TransactionSnapshotRecord? record)
        {
            if (Saved.TryGetValue(transactionId, out var found))
            {
                record = found;
                return true;
            }
            record = null;
            return false;
        }

        public bool TryLoadLatestForOptimization(string optimizationId, out TransactionSnapshotRecord? record)
        {
            record = Saved.Values
                .Where(r => r.Manifest.OptimizationId.Equals(optimizationId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.Manifest.TimestampUtc)
                .FirstOrDefault();
            return record is not null;
        }

        public void Delete(Guid transactionId) => Saved.Remove(transactionId);

        public IReadOnlyList<TransactionSnapshotRecord> ListAll() => Saved.Values.ToList();

        public bool TryLoadLegacy(string optimizationId, out OptimizationSnapshot? snapshot)
        {
            snapshot = null;
            return false;
        }

        public void DeleteLegacy(string optimizationId) { }

        public IReadOnlyList<string> ListLegacyIds() => Array.Empty<string>();
    }

    [Fact]
    public void JournalWithoutTerminalEventIsIncomplete()
    {
        var journal = NewJournal();
        var txid = Guid.NewGuid();
        journal.Append(new TransactionEvent(txid, "maximum-power-plan",
            DateTime.UtcNow, TransactionPhase.Snapshot, Terminal: false, null));

        var incomplete = Assert.Single(journal.Incomplete());

        Assert.Equal(txid, incomplete.TransactionId);
        Assert.Equal("maximum-power-plan", incomplete.OptimizationId);
    }

    [Fact]
    public void CommittedTransactionIsNeverIncomplete()
    {
        var journal = NewJournal();
        var txid = Guid.NewGuid();
        journal.Append(new TransactionEvent(txid, "disable-vbs", DateTime.UtcNow,
            TransactionPhase.Snapshot, false, null));
        journal.Append(new TransactionEvent(txid, "disable-vbs", DateTime.UtcNow,
            TransactionPhase.Commit, true, null));

        Assert.Empty(journal.Incomplete());
    }

    [Fact]
    public void CorruptLineInsideJournalDoesNotLoseOtherTransactions()
    {
        var journal = new FileTransactionJournal(_dir);
        var good = Guid.NewGuid();
        journal.Append(new TransactionEvent(good, "disable-transparency",
            DateTime.UtcNow, TransactionPhase.Snapshot, false, null));
        File.AppendAllText(Path.Combine(_dir, "not-a-guid.jsonl"), "{}");
        File.AppendAllText(
            Path.Combine(_dir, good.ToString("D") + ".jsonl"),
            "{broken\n");

        var incomplete = Assert.Single(journal.Incomplete());
        Assert.Equal(good, incomplete.TransactionId);
    }

    [Fact]
    public void RecoveryCompletedClosesTheLoop()
    {
        var journal = NewJournal();
        var txid = Guid.NewGuid();
        journal.Append(new TransactionEvent(txid, "zero-menu-delay",
            DateTime.UtcNow, TransactionPhase.Apply, false, null));
        Assert.Single(journal.Incomplete());

        journal.Append(new TransactionEvent(txid, "zero-menu-delay",
            DateTime.UtcNow, TransactionPhase.RecoveryCompleted, true, null));

        Assert.Empty(journal.Incomplete());
    }

    [Fact]
    public void ApplyReachedWithLiveAppliedStateRequiresRollback()
    {
        var journal = NewJournal();
        var store = NewStore();
        var txid = Guid.NewGuid();

        journal.Append(new TransactionEvent(txid, "lock-test", DateTime.UtcNow,
            TransactionPhase.Snapshot, false, null));
        store.Save(new TransactionSnapshotRecord
        {
            Manifest = new TransactionSnapshotManifest
            {
                TransactionId = txid,
                OptimizationId = "lock-test",
                DefinitionVersion = "1",
                SchemaVersion = 3,
                AppVersion = AppVersion.Semantic,
                WindowsBuild = 26200,
                TimestampUtc = DateTime.UtcNow,
            },
            State = new OptimizationSnapshot(),
        });
        journal.Append(new TransactionEvent(txid, "lock-test", DateTime.UtcNow.AddSeconds(1),
            TransactionPhase.Apply, false, null));

        // Live state shows the change APPLIED: rollback required.
        var service = new CrashRecoveryService(journal, store, _ => OptimizationState.AppliedByCao);

        var candidate = Assert.Single(service.Scan());
        Assert.Equal(RecoveryDecision.RollbackRequired, candidate.Decision);
        Assert.True(candidate.HasSnapshot);

        // After the caller reverts and closes the loop: clean.
        service.MarkRecovered(txid, "lock-test", recovered: true);
        Assert.DoesNotContain(service.Scan(), candidate => candidate.TransactionId == txid);
    }

    [Fact]
    public void ApplyNotReachedIsSafeToIgnore()
    {
        var journal = NewJournal();
        var store = NewStore();
        var txid = Guid.NewGuid();
        journal.Append(new TransactionEvent(txid, "safe-opt", DateTime.UtcNow,
            TransactionPhase.Precheck, false, null));

        var service = new CrashRecoveryService(journal, store, _ => OptimizationState.NotApplied);

        var candidate = Assert.Single(service.Scan());
        Assert.Equal(RecoveryDecision.SafeToIgnore, candidate.Decision);
    }

    [Fact]
    public void MissingSnapshotAfterApplyIsCorrupted()
    {
        var journal = NewJournal();
        var store = NewStore();
        var txid = Guid.NewGuid();
        journal.Append(new TransactionEvent(txid, "ghost-opt", DateTime.UtcNow,
            TransactionPhase.Snapshot, false, null));
        journal.Append(new TransactionEvent(txid, "ghost-opt", DateTime.UtcNow,
            TransactionPhase.Apply, false, null));
        // NOTE: no snapshot saved in the store — crash lost it.

        var service = new CrashRecoveryService(journal, store, _ => OptimizationState.Unknown);

        var candidate = Assert.Single(service.Scan());
        Assert.Equal(RecoveryDecision.Corrupted, candidate.Decision);
    }

    [Fact]
    public void HasPendingRecoveryBlocksNewMutationsOnlyForSeriousDecisions()
    {
        var journal = NewJournal();
        var store = NewStore();
        var service = new CrashRecoveryService(journal, store, _ => OptimizationState.NotApplied);

        var safeTx = Guid.NewGuid();
        journal.Append(new TransactionEvent(safeTx, "safe-opt", DateTime.UtcNow,
            TransactionPhase.Precheck, false, null));
        Assert.False(service.HasPendingRecovery()); // SafeToIgnore does not block

        // Part 2: same journal, but live state now shows the change APPLIED.
        var seriousTx = Guid.NewGuid();
        store.Save(new TransactionSnapshotRecord
        {
            Manifest = new TransactionSnapshotManifest
            {
                TransactionId = seriousTx,
                OptimizationId = "serious-opt",
                DefinitionVersion = "1",
                SchemaVersion = 3,
                AppVersion = AppVersion.Semantic,
                WindowsBuild = 26200,
                TimestampUtc = DateTime.UtcNow,
            },
            State = new OptimizationSnapshot(),
        });
        journal.Append(new TransactionEvent(seriousTx, "serious-opt", DateTime.UtcNow,
            TransactionPhase.Apply, false, null));

        var blockingService = new CrashRecoveryService(journal, store,
            _ => OptimizationState.AppliedByCao);
        Assert.True(blockingService.HasPendingRecovery());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
