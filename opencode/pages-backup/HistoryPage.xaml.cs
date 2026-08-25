using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>History viewer backed by %ProgramData%\CA-O\history.jsonl (spec 74).</summary>
public sealed partial class HistoryPage : Page
{
    public sealed record HistoryRow(string TimeLabel, string Operation, string OptimizationId, bool Success, string Detail, string Verification)
    {
        public string OutcomeLabel => Success ? "OK" : "FALLO";

        public Brush OutcomeBrush => Success
            ? new SolidColorBrush(Colors.ForestGreen)
            : new SolidColorBrush(Colors.IndianRed);
    }

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

    private void Refresh()
    {
        var entries = AppServices.History.ReadLast(200);
        HistoryList.ItemsSource = entries.Select(entry => new HistoryRow(
            entry.TimestampUtc.ToLocalTime().ToString("g"),
            entry.Operation,
            entry.OptimizationId,
            entry.Success,
            BuildDetail(entry),
            entry.Verification ?? "-")).ToList();
    }

    private static string BuildDetail(HistoryEntry entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(entry.Precondition))
        {
            parts.Add($"precheck={entry.Precondition}");
        }
        if (!string.IsNullOrEmpty(entry.ApplyResult))
        {
            parts.Add($"apply={entry.ApplyResult}");
        }
        if (!string.IsNullOrEmpty(entry.Verification))
        {
            parts.Add($"verify={entry.Verification}");
        }
        parts.Add(entry.RollbackAvailable ? "rollback disponible" : "sin rollback");
        if (!string.IsNullOrEmpty(entry.Error))
        {
            parts.Add($"error: {entry.Error}");
        }
        return string.Join(" · ", parts);
    }
}
