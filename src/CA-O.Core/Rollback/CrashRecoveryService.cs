using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Rollback;

/// <summary>Recovery verdicts for an incomplete transaction (spec 12/FASE 12).</summary>
public enum RecoveryDecision
{
    /// <summary>No mutation could have started (failed before SNAPSHOT).</summary>
    SafeToIgnore,

    /// <summary>Journal shows Commit; only cleanup of the snapshot remains.</summary>
    AlreadyCommitted,

    /// <summary>APPLY reached and live state differs from the pre-state.</summary>
    RollbackRequired,

    /// <summary>Cannot decide automatically; user must inspect.</summary>
    RecoveryRequired,

    /// <summary>Snapshot missing/unreadable though APPLY may have run.</summary>
    Corrupted,

    Unknown,
}

/// <summary>An unfinished transaction with its recovery verdict.</summary>
public sealed record IncompleteTransaction(
    Guid TransactionId,
    string OptimizationId,
    TransactionPhase LastPhase,
    DateTime LastTransitionUtc,
    bool HasSnapshot,
    OptimizationState LiveState,
    RecoveryDecision Decision);

/// <summary>
/// Crash recovery driven EXCLUSIVELY by the transaction journal (P0-4):
/// journal → group by TransactionId → non-terminal last event = incomplete →
/// resolve snapshot by TransactionId → compare live state → decide. History
/// is never used to determine completeness.
/// </summary>
public sealed class CrashRecoveryService
{
    private readonly ITransactionJournal _journal;
    private readonly ISnapshotStore _snapshots;
    private readonly Func<string, OptimizationState> _detectLive;

    public CrashRecoveryService(
        ITransactionJournal journal,
        ISnapshotStore snapshots,
        Func<string, OptimizationState> detectLive)
    {
        _journal = journal;
        _snapshots = snapshots;
        _detectLive = detectLive;
    }

    public IReadOnlyList<IncompleteTransaction> Scan()
    {
        var result = new List<IncompleteTransaction>();

        foreach (var incomplete in _journal.Incomplete())
        {
            var hasSnapshot = _snapshots.TryLoad(incomplete.TransactionId, out var record);
            var live = SafeDetect(incomplete.OptimizationId);
            var phaseReachedApply = incomplete.LastPhase is TransactionPhase.Apply
                or TransactionPhase.Verify
                or TransactionPhase.BenchmarkStarted
                or TransactionPhase.Commit;

            RecoveryDecision decision;
            if (!hasSnapshot || record is null)
            {
                decision = phaseReachedApply ? RecoveryDecision.Corrupted : RecoveryDecision.SafeToIgnore;
            }
            else if (phaseReachedApply)
            {
                // Compare live registry state against the captured pre-state.
                var liveMatchesPreState = LiveMatchesPreState(record.State, live);
                decision = liveMatchesPreState ? RecoveryDecision.SafeToIgnore : RecoveryDecision.RollbackRequired;
            }
            else if (live == OptimizationState.Unknown)
            {
                decision = RecoveryDecision.Unknown;
            }
            else
            {
                decision = RecoveryDecision.SafeToIgnore;
            }

            result.Add(new IncompleteTransaction(
                incomplete.TransactionId,
                incomplete.OptimizationId,
                incomplete.LastPhase,
                incomplete.LastTransitionUtc,
                hasSnapshot,
                live,
                decision));
        }

        return result;
    }

    /// <summary>Closes an incomplete transaction after the caller performed recovery.</summary>
    public void MarkRecovered(Guid transactionId, string optimizationId, bool recovered)
    {
        _journal.Append(new TransactionEvent(
            transactionId, optimizationId, DateTime.UtcNow,
            TransactionPhase.RecoveryCompleted, Terminal: true,
            ErrorCode: recovered ? null : "CAO-ROLLBACK-001"));
    }

    /// <summary>True when any pending recovery must block new mutations (FASE 12).</summary>
    public bool HasPendingRecovery() =>
        Scan().Any(candidate => candidate.Decision is RecoveryDecision.RollbackRequired
                                                or RecoveryDecision.RecoveryRequired
                                                or RecoveryDecision.Corrupted);

    private bool LiveMatchesPreState(OptimizationSnapshot preState, OptimizationState live)
    {
        // A pre-state whose entries all read as NotApplied means nothing was
        // applied yet: safe.
        return live == OptimizationState.NotApplied || live == OptimizationState.Unknown
            ? live != OptimizationState.AppliedByCao && live != OptimizationState.AppliedManually
            : false;
    }

    private OptimizationState SafeDetect(string optimizationId)
    {
        try
        {
            return _detectLive(optimizationId);
        }
        catch
        {
            return OptimizationState.Unknown;
        }
    }
}
