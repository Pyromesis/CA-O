using CAO.Shared;

namespace CAO.Core.Rollback;

/// <summary>
/// Append-only persistence of transaction transitions (FASE 5/12). The crash
/// recovery engine reads journals to find transactions without a terminal
/// event; the service refuses new dangerous mutations while any exist.
/// </summary>
public interface ITransactionJournal
{
    void Append(TransactionEvent evt);

    /// <summary>All events grouped by transaction, ordered by timestamp.</summary>
    IReadOnlyList<(Guid TransactionId, IReadOnlyList<TransactionEvent> Events)> LoadAll();
}

/// <summary>A transaction whose last journal event is not terminal.</summary>
public sealed record UnfinishedTransaction(
    Guid TransactionId,
    string OptimizationId,
    TransactionPhase LastPhase,
    DateTime LastTransitionUtc);

public static class TransactionJournalExtensions
{
    /// <summary>
    /// Transactions whose last APPENDED event is not terminal (spec 122).
    /// "Last" = last line in the journal file (append order), never wall-clock.
    /// </summary>
    public static IReadOnlyList<UnfinishedTransaction> Incomplete(this ITransactionJournal journal) =>
        journal.LoadAll()
            .Select(group => (group.TransactionId, Last: group.Events[^1]))
            .Where(item => !TransactionEvent.IsTerminal(item.Last.Phase))
            .Select(item => new UnfinishedTransaction(
                item.TransactionId,
                item.Last.OptimizationId,
                item.Last.Phase,
                item.Last.TimestampUtc))
            .ToList();
}
