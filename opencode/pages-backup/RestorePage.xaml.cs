using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Restore page (spec 72-73): shows CA-O snapshots (rollback layer 2) and
/// explains the Windows restore-point relationship (layer 1) honestly.
/// </summary>
public sealed partial class RestorePage : Page
{
    public RestorePage()
    {
        InitializeComponent();
        NoteBar.Message = Localizer.Get("restore.pointNote");
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var directory = CaOPaths.SnapshotsDirectory;
        if (!Directory.Exists(directory))
        {
            SnapshotsList.ItemsSource = new[] { "Todavía no hay snapshots guardados." };
            return;
        }

        var files = Directory.GetFiles(directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SnapshotsList.ItemsSource = files.Count == 0
            ? new[] { "Todavía no hay snapshots guardados." }
            : files;
    }
}
