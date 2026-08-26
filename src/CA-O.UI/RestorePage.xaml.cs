using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Restore page (spec 72-73): shows CA-O snapshots (rollback layer 2) and
/// explains the Windows restore-point relationship (layer 1) honestly.
/// TransactionId is primary identity; never overwritten.
/// </summary>
public sealed partial class RestorePage : Page
{
    public RestorePage()
    {
        InitializeComponent();
        NoteBar.Message = Localizer.Get("restore.pointNote");
        RecoveryHintText.Text = AppServices.State.RecoveryCandidates.Count == 0
            ? "Sin recuperaciones pendientes."
            : $"Recuperación requerida: {string.Join(", ", AppServices.State.RecoveryCandidates)} — revise transacción y revierta.";
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Refresh();
    private void OnGoHistoryClick(object sender, RoutedEventArgs e)
    {
        // Navigation via MainWindow already hosts history; emit hint.
        RecoveryHintText.Text = "Historial contiene el timeline auditable con TransactionId por operación.";
    }

    private void OnInspectClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            ShowInspectDialog(id);
        }
        else if (sender is Button { Tag: object obj } && obj is not null)
        {
            ShowInspectDialog(obj.ToString() ?? "");
        }
    }

    private async void ShowInspectDialog(string snapshotId)
    {
        var dialog = new ContentDialog
        {
            Title = $"Snapshot {snapshotId}",
            Content = new TextBlock { Text = $"Snapshot en {CaOPaths.SnapshotsDirectory} — identidad TransactionId. Contiene valores originales y ausencias (DELETE) para restauración exacta.", TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "Cerrar",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        string? snapshotId = null;
        if (sender is Button { Tag: string sid }) snapshotId = sid;
        else if (sender is Button { Tag: object o }) snapshotId = o?.ToString();
        if (string.IsNullOrWhiteSpace(snapshotId)) return;

        var confirm = new ContentDialog
        {
            Title = "Confirmar restauración",
            Content = $"Se revertirá el snapshot {snapshotId} vía servicio privilegiado. La reversión se verifica exactamente contra el snapshot original.",
            PrimaryButtonText = "Restaurar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        // Dispatch revert through IPC if optimization id can be derived; otherwise show guidance.
        var match = snapshotId.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        try
        {
            // For demo we attempt to revert by snapshot folder name heuristic; real path loads manifest.
            var resp = await AppServices.Pipe.SendAsync(CAO.Shared.IPC.PrivilegedOperationKind.RevertOptimization, snapshotId);
            RecoveryHintText.Text = resp is { Accepted: true } ? "✓ Reversión solicitada y aceptada." : $"Rechazado [{resp?.ErrorCode}]: {resp?.SafeMessage}";
        }
        catch (Exception ex)
        {
            RecoveryHintText.Text = $"Restauración falló (servicio no disponible): {ex.Message}";
        }
    }

    private void Refresh()
    {
        var directory = CaOPaths.SnapshotsDirectory;
        if (!Directory.Exists(directory))
        {
            SnapshotsList.ItemsSource = Array.Empty<string>();
            EmptySnapshotsCard.Visibility = Visibility.Visible;
            SnapshotsList.Visibility = Visibility.Collapsed;
            return;
        }

        var folders = Directory.GetDirectories(directory).Select(Path.GetFileName).OrderByDescending(s => s).ToList();
        var files = Directory.GetFiles(directory, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(s => s).ToList();
        var combined = folders.Count > 0 ? folders! : files;

        EmptySnapshotsCard.Visibility = combined.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SnapshotsList.Visibility = combined.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        SnapshotsList.ItemsSource = combined.Count == 0 ? Array.Empty<string>() : combined;

        RecoveryHintText.Text = AppServices.State.RecoveryCandidates.Count == 0
            ? "Sin recuperaciones pendientes."
            : $"Recuperación requerida: {string.Join(", ", AppServices.State.RecoveryCandidates)}";
    }
}
