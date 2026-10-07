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
            var phaseReachedApply = incomplete.LastPhase is TransactionPhase.Apply
                or TransactionPhase.Verify
                or TransactionPhase.BenchmarkStarted
                or TransactionPhase.Commit;

            // CAO-BUG-2026-10-06: la deteccion en vivo se hace DESPUES de comprobar
            // si la transaccion llego a Apply. Sin Apply el veredicto solo puede ser
            // SafeToIgnore/Unknown (ninguno bloquea) y un Unknown ya lo produce un
            // snapshot ausente o ilegible, asi que la deteccion no puede cambiar el
            // resultado. Antes se ejecutaba siempre y en la familia de limpieza eso
            // significa recorrer el arbol de ficheros entero: con el historial real
            // (88 entradas, 22 de cleanup-windows-temp) esta llamada bloqueaba el
            // hilo de UI durante minutos en cada Analizar del Dashboard.
            var live = phaseReachedApply ? SafeDetect(incomplete.OptimizationId) : OptimizationState.Unknown;

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
    /// <remarks>
    /// Equivalent to filtering <see cref="Scan"/> by the blocking decisions, but
    /// without paying for live-state detection that cannot change the outcome.
    /// A transaction that never reached Apply resolves to SafeToIgnore or
    /// Unknown, and neither blocks, so it is skipped without detecting.
    /// CAO-BUG-2026-10-06: this runs on the privileged service dispatch thread on
    /// every apply; detection is a full recursive disk enumeration that costs
    /// seconds, and the journal accumulates one stuck entry per successful apply.
    /// </remarks>
    public bool HasPendingRecovery()
    {
        foreach (var incomplete in _journal.Incomplete())
        {
            // Same set as the phaseReachedApply test in Scan(): without a snapshot
            // the outcome is Corrupted only when Apply was reached, and with a
            // snapshot it is SafeToIgnore/Unknown. None of those block.
            if (incomplete.LastPhase is not (TransactionPhase.Apply
                or TransactionPhase.Verify
                or TransactionPhase.BenchmarkStarted
                or TransactionPhase.Commit))
            {
                continue;
            }

            if (!_snapshots.TryLoad(incomplete.TransactionId, out var record) || record is null)
            {
                return true; // Corrupted
            }

            if (!LiveMatchesPreState(record.State, SafeDetect(incomplete.OptimizationId)))
            {
                return true; // RollbackRequired
            }
        }

        return false;
    }

    private bool LiveMatchesPreState(OptimizationSnapshot preState, OptimizationState live)
    {
        // Fail-safe: solo es seguro ignorar si el pre-estado estaba vacío
        // (nada observable que comparar) Y el estado vivo dice NotApplied.
        // Si el snapshot contiene entradas o notas, el Detect solo no basta:
        // se exige rollback contra el snapshot real.
        if (live == OptimizationState.Unknown) return false;
        if (live != OptimizationState.NotApplied) return false;
        return preState.Registry.Count == 0
            && preState.ServiceStartTypes.Count == 0
            && preState.RawNotes.Count == 0;
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
