using CAO.Core.Overclock.Stability;
using CAO.Infrastructure.Persistence;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// The tuning engine's journal, backed by the same append-only transaction file
/// the rest of the app uses. The run id is the transaction id, so a tuning run
/// shows up in the journal next to every optimization, and it never closes:
/// closing is the caller's job, and a tuning that closed its own transaction
/// could report a terminal state nobody verified.
/// </summary>
public sealed class TransactionOverclockJournal : IOverclockJournal
{
    public const string OptimizationId = "overclock-tune";

    private readonly FileTransactionJournal _inner;
    private readonly Guid _runId;

    public TransactionOverclockJournal(FileTransactionJournal inner, Guid runId)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _runId = runId;
    }

    public ValueTask WriteAsync(TransactionPhase phase, string? detail, CancellationToken cancellationToken)
    {
        // The engine calls from a worker thread; Append flushes to disk inline.
        _inner.Append(new TransactionEvent(
            _runId,
            OptimizationId,
            DateTime.UtcNow,
            phase,
            Terminal: false,
            detail));
        return ValueTask.CompletedTask;
    }
}
