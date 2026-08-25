using CAO.Core.Rollback;
using CAO.Infrastructure.Persistence;
using CAO.Shared;
using Xunit;

namespace CAO.Integration.Tests;

/// <summary>
/// Transaction-journal recovery (FASE 12): a journal without a terminal
/// event is INCOMPLETE; recovery appends the closing event and unblocks new
/// mutations.
/// </summary>
public sealed class TransactionJournalRecoveryTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "cao-txn-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void JournalWithoutTerminalEventIsIncomplete()
    {
        var journal = new FileTransactionJournal(_dir);
        var txid = Guid.NewGuid();
        journal.Append(new TransactionEvent(txid, "maximum-power-plan",
            DateTime.UtcNow, TransactionPhase.Snapshot, Terminal: false, null));

        var incomplete = Assert.Single(journal.Incomplete());

        Assert.Equal(txid, incomplete.TransactionId);
        Assert.Equal("maximum-power-plan", incomplete.OptimizationId);
        Assert.Equal(TransactionPhase.Snapshot, incomplete.LastPhase);
    }

    [Fact]
    public void CommittedTransactionIsNeverIncomplete()
    {
        var journal = new FileTransactionJournal(_dir);
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

        // The good transaction survives; the corrupt trailing line is ignored.
        var incomplete = Assert.Single(journal.Incomplete());
        Assert.Equal(good, incomplete.TransactionId);
    }

    [Fact]
    public void RecoveryCompletedClosesTheLoop()
    {
        var journal = new FileTransactionJournal(_dir);
        var txid = Guid.NewGuid();
        journal.Append(new TransactionEvent(txid, "zero-menu-delay",
            DateTime.UtcNow, TransactionPhase.Apply, false, null));
        Assert.Single(journal.Incomplete());

        journal.Append(new TransactionEvent(txid, "zero-menu-delay",
            DateTime.UtcNow, TransactionPhase.RecoveryCompleted, true, null));

        Assert.Empty(journal.Incomplete());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_tempGuard(), recursive: true);
        }
    }

    private string _tempGuard() => _dir;
}
