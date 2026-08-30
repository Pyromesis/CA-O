using CAO.Shared;

namespace CAO.Infrastructure.Persistence;

/// <summary>
/// Central singleton service for analysis persistence + freshness + game fingerprint.
/// Single source of truth — avoids duplication across Dashboard/Analyze/Optimize.
/// </summary>
public sealed class AnalysisSessionService
{
    private readonly AnalysisStateStore _store;

    public AnalysisSessionService(AnalysisStateStore store) => _store = store;

    public AnalysisStateStore.PersistedAnalysis? LoadLatest() => _store.LoadLatestAnalysis();

    public void Save(AnalysisStateStore.PersistedAnalysis analysis) => _store.SaveAnalysis(analysis);

    public void Clear() => _store.DeleteAnalysis();

    public AnalysisStateStore.PersistedAnalysis? GetLastAnalysis() => _store.LoadLatestAnalysis();

    public TimeSpan GetAnalysisAge(AnalysisStateStore.PersistedAnalysis? a) => a == null ? TimeSpan.Zero : DateTime.UtcNow - a.TimestampUtc;

    public (AnalysisFreshness Freshness, StaleReason Reason, TimeSpan Age) GetFreshness(SystemContext? currentContext = null, string? currentGamesFingerprint = null)
    {
        var latest = _store.LoadLatestAnalysis();
        return _store.GetFreshness(latest, currentContext, currentGamesFingerprint);
    }

    public bool IsStale(SystemContext? currentContext = null, string? currentGamesFingerprint = null)
    {
        var (f, _, _) = GetFreshness(currentContext, currentGamesFingerprint);
        return f is AnalysisFreshness.Stale or AnalysisFreshness.VeryStale;
    }

    public StaleReason DetectStaleness(SystemContext? currentContext, string? currentGamesFingerprint)
    {
        var (_, reason, _) = GetFreshness(currentContext, currentGamesFingerprint);
        return reason;
    }

    public static string ComputeGamesFingerprint(IReadOnlyList<string> games) => AnalysisStateStore.ComputeGamesFingerprint(games);

    public DateTime? LastAnalysisUtc => _store.LoadLatestAnalysis()?.TimestampUtc;
    public DateTime? NextRecommendedAnalysisUtc
    {
        get
        {
            var last = _store.LoadLatestAnalysis();
            return last == null ? null : last.TimestampUtc.AddDays(7);
        }
    }

    public void MarkStale() { /* freshness is computed, no flag needed */ }
}
