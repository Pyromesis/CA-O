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

/// <summary>An unfinished transaction detected by scanning journals.</summary>
public sealed record IncompleteTransaction(
    Guid TransactionId,
    string OptimizationId,
    TransactionPhase LastPhase,
    DateTime LastTransitionUtc);

public static class TransactionJournalExtensions
{
    /// <summary>Transactions whose last event is not terminal (spec 122).</summary>
    public static IReadOnlyList<IncompleteTransaction> Incomplete(this ITransactionJournal journal) =>
        journal.LoadAll()
            .Select(group => (group.TransactionId, Last: group.Events.OrderBy(e => e.TimestampUtc).Last()))
            .Where(item => !TransactionEvent.IsTerminal(item.Last.Phase))
            .Select(item => new IncompleteTransaction(
                item.TransactionId,
                item.Last.OptimizationId,
                item.Last.Phase,
                item.Last.TimestampUtc))
            .ToList();
}
