using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>History viewer backed by %ProgramData%\CA-O\history.jsonl (spec 74) with filters and search.</summary>
public sealed partial class HistoryPage : Page
{
    public sealed record HistoryRow(string TimeLabel, string Operation, string OptimizationId, bool Success, string Detail, string TransactionHint, string StatusLabel, Brush StatusBrush, string SearchKey);

    private List<HistoryRow> _allRows = new();
    private string _filter = "all";
    private string _search = "";

    public HistoryPage()
    {
        InitializeComponent();
        SourceNote.Text = $"Fuente: {CaOPaths.HistoryFile}";
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Refresh();
    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterBox.SelectedItem is ComboBoxItem { Tag: string tag }) _filter = tag;
        ApplyFilter();
    }
    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text?.Trim() ?? "";
        ApplyFilter();
    }

    private void Refresh()
    {
        var entries = AppServices.History.ReadLast(200);
        _allRows = entries.Select(entry => new HistoryRow(
            entry.TimestampUtc.ToLocalTime().ToString("g"),
            entry.Operation,
            entry.OptimizationId,
            entry.Success,
            BuildDetail(entry),
            $"TX: {entry.SnapshotId ?? "—"} · {entry.TimestampUtc:O}",
            entry.Success ? "OK" : "FALLO",
            entry.Success ? new SolidColorBrush(Colors.ForestGreen) : new SolidColorBrush(Colors.IndianRed),
            $"{entry.OptimizationId} {entry.Operation} {entry.SnapshotId} {entry.Error}".ToLowerInvariant())).ToList();
        EmptyHistoryCard.Visibility = _allRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryList.Visibility = _allRows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (_allRows is null) return;
        var filtered = _allRows.Where(row =>
        {
            bool filterOk = _filter switch
            {
                "optimization" => row.Operation.Contains("apply", StringComparison.OrdinalIgnoreCase),
                "rollback" => row.Operation.Contains("revert", StringComparison.OrdinalIgnoreCase) || row.Operation.Contains("rollback", StringComparison.OrdinalIgnoreCase),
                "error" => !row.Success,
                "security" => row.Operation.Contains("security", StringComparison.OrdinalIgnoreCase),
                _ => true
            };
            bool searchOk = string.IsNullOrWhiteSpace(_search) || row.SearchKey.Contains(_search, StringComparison.OrdinalIgnoreCase);
            return filterOk && searchOk;
        }).ToList();
        HistoryList.ItemsSource = filtered;
    }

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
