using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using CAO.Shared;

namespace CAO.UI.ViewModels;

public sealed record HistoryRow(string TimeLabel, string Operation, string OptimizationId, bool Success, string Detail, string TransactionHint, string StatusLabel, Brush StatusBrush, string SearchKey);

/// <summary>ViewModel para HistoryPage — auditoría con filtros y búsqueda (§72).</summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly Infrastructure.Logging.JsonHistoryLogger _history;

    public HistoryViewModel(Infrastructure.Logging.JsonHistoryLogger history) => _history = history;

    [ObservableProperty] private IReadOnlyList<HistoryRow> _rows = Array.Empty<HistoryRow>();
    [ObservableProperty] private IReadOnlyList<HistoryRow> _filteredRows = Array.Empty<HistoryRow>();
    [ObservableProperty] private string _filter = "all";
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _warningMessage = string.Empty;
    [ObservableProperty] private int _corruptedCount;

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            var warnings = _history.VerifyIntegrity();
            CorruptedCount = warnings.Count;
            WarningMessage = warnings.Count == 0 ? string.Empty : $"{warnings.Count} entradas no pudieron leerse (corruptas) — se omitieron. Historial disponible con {Math.Max(0, 200 - warnings.Count)} registros válidos.";
            var entries = _history.ReadLast(200);
            Rows = entries.Select(entry => new HistoryRow(
                entry.TimestampUtc.ToLocalTime().ToString("g"),
                entry.Operation,
                entry.OptimizationId,
                entry.Success,
                BuildDetail(entry),
                $"TX: {entry.SnapshotId ?? "—"} · {entry.TimestampUtc:O}",
                entry.Success ? "OK" : "FALLO",
                entry.Success ? new SolidColorBrush(Colors.ForestGreen) : new SolidColorBrush(Colors.IndianRed),
                $"{entry.OptimizationId} {entry.Operation} {entry.SnapshotId} {entry.Error}".ToLowerInvariant())).ToList();
            IsEmpty = Rows.Count == 0;
        }
        catch (Exception ex)
        {
            // Nunca cerrar la app (§19, §43)
            WarningMessage = $"No se pudo cargar el historial: {ex.GetType().Name} — se muestra lo recuperable.";
            try { Rows = _history.ReadLast(200).Select(entry => new HistoryRow(entry.TimestampUtc.ToLocalTime().ToString("g"), entry.Operation, entry.OptimizationId, entry.Success, BuildDetail(entry), $"TX: {entry.SnapshotId ?? "—"}", entry.Success ? "OK" : "FALLO", new SolidColorBrush(entry.Success ? Colors.ForestGreen : Colors.IndianRed), "")).ToList(); } catch { Rows = Array.Empty<HistoryRow>(); }
            IsEmpty = Rows.Count == 0;
            try { App.WriteCrashLog(ex); } catch { }
        }
        ApplyFilter();
    }

    [RelayCommand]
    private void ApplyFilter()
    {
        if (Rows is null) { FilteredRows = Array.Empty<HistoryRow>(); return; }
        var filtered = Rows.Where(row =>
        {
            bool filterOk = Filter switch
            {
                "optimization" => row.Operation.Contains("apply", StringComparison.OrdinalIgnoreCase),
                "rollback" => row.Operation.Contains("revert", StringComparison.OrdinalIgnoreCase) || row.Operation.Contains("rollback", StringComparison.OrdinalIgnoreCase),
                "error" => !row.Success,
                "security" => row.Operation.Contains("security", StringComparison.OrdinalIgnoreCase),
                _ => true
            };
            bool searchOk = string.IsNullOrWhiteSpace(Search) || row.SearchKey.Contains(Search, StringComparison.OrdinalIgnoreCase);
            return filterOk && searchOk;
        }).ToList();
        FilteredRows = filtered;
    }

    partial void OnFilterChanged(string value) => ApplyFilter();
    partial void OnSearchChanged(string value) => ApplyFilter();

    private static string BuildDetail(HistoryEntry entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(entry.Precondition)) parts.Add($"precheck={entry.Precondition}");
        if (!string.IsNullOrEmpty(entry.ApplyResult)) parts.Add($"apply={entry.ApplyResult}");
        if (!string.IsNullOrEmpty(entry.Verification)) parts.Add($"verify={entry.Verification}");
        parts.Add(entry.RollbackAvailable ? "rollback disponible" : "sin rollback");
        if (!string.IsNullOrEmpty(entry.Error)) parts.Add($"error: {entry.Error}");
        if (!string.IsNullOrEmpty(entry.BenchmarkSummary)) parts.Add($"bench: {entry.BenchmarkSummary}");
        return string.Join(" · ", parts);
    }
}
