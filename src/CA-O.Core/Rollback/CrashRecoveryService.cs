using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Rollback;

/// <summary>A change left in an undetermined state by a crashed run (spec 13).</summary>
public sealed record RecoveryCandidate(
    string OptimizationId,
    DateTime SnapshotUtc,
    OptimizationState LiveState,
    string ReasonEs);

/// <summary>
/// Crash recovery engine (spec 13). A snapshot that exists on disk while the
/// history shows no committed+verified apply for the same optimization means
/// the process died mid-operation: state is Incomplete. The engine never
/// assumes "apply == success"; it compares the live state and offers
/// rollback through the caller (service/UI), then MarkRecovered closes it.
/// </summary>
public sealed class CrashRecoveryService
{
    private readonly ISnapshotStore _store;
    private readonly IHistoryLogger _history;
    private readonly Func<string, OptimizationState> _detectLive;

    public CrashRecoveryService(
        ISnapshotStore store,
        IHistoryLogger history,
        Func<string, OptimizationState> detectLive)
    {
        _store = store;
        _history = history;
        _detectLive = detectLive;
    }

    /// <summary>Scans persisted snapshots against history to find incomplete operations.</summary>
    public IReadOnlyList<RecoveryCandidate> Scan()
    {
        var candidates = new List<RecoveryCandidate>();
        var entriesByOptimization = _history.ReadLast(int.MaxValue)
            .GroupBy(entry => entry.OptimizationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var snapshotId in _store.ListIds())
        {
            if (!_store.TryLoad(snapshotId, out var snapshot))
            {
                continue;
            }

            entriesByOptimization.TryGetValue(snapshotId, out var entries);
            var last = entries?.OrderBy(entry => entry.TimestampUtc).LastOrDefault();

            var committed = last is not null &&
                            last.Operation is "apply" or "recovery" &&
                            last.Success &&
                            last.Verification is "passed" or "pending-reboot";

            if (committed)
            {
                continue; // healthy: change applied, verified, snapshot kept for future rollback
            }

            candidates.Add(new RecoveryCandidate(
                snapshotId,
                snapshot.TimestampUtc,
                SafeDetect(snapshotId),
                ReasonFor(last)));
        }

        return candidates;
    }

    /// <summary>Closes a recovery after the caller performed the actual revert.</summary>
    public void MarkRecovered(string optimizationId, bool revertSucceeded)
    {
        _history.Log(new HistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            AppVersion = AppVersion.Semantic,
            User = Environment.UserName,
            OptimizationId = optimizationId,
            Operation = "recovery",
            Success = revertSucceeded,
            Verification = revertSucceeded ? "passed" : "failed",
            Error = revertSucceeded ? null : "La reversión de recuperación falló; revise el estado manualmente.",
        });

        if (revertSucceeded)
        {
            _store.Delete(optimizationId);
        }
    }

    /// <summary>True when the live system no longer reflects the applied change.</summary>
    public bool LiveStateMatchesSnapshotExpectation(RecoveryCandidate candidate) =>
        candidate.LiveState is OptimizationState.NotApplied or OptimizationState.Unknown;

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

    private static string ReasonFor(HistoryEntry? last) => last switch
    {
        null => "Snapshot sin registro de operación: el proceso murió antes o durante APPLY.",
        { Operation: "precheck" } => "El proceso murió después del PRECHECK y antes de APPLY.",
        { Success: false } => $"Última operación '{last.Operation}' terminó en fallo sin cierre limpio.",
        _ => "Última operación sin verificación registrada.",
    };
}
