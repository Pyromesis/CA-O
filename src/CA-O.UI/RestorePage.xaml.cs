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
    private readonly ViewModels.RestoreViewModel _vm;

    public RestorePage()
    {
        InitializeComponent();
        NoteBar.Message = Localizer.Get("restore.pointNote");
        _vm = AppHost.Resolve<ViewModels.RestoreViewModel>();
        DataContext = _vm;
        _vm.RefreshCommand.Execute(null);
        RecoveryHintText.Text = _vm.RecoveryHint;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.RestoreViewModel.Snapshots) or nameof(ViewModels.RestoreViewModel.RecoveryHint) or nameof(ViewModels.RestoreViewModel.IsEmpty))
                DispatcherQueue.TryEnqueue(RenderVm);
        };
        RenderVm();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _vm.RefreshCommand.Execute(null);
    }

    private void RenderVm()
    {
        EmptySnapshotsCard.Visibility = _vm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        SnapshotsList.Visibility = _vm.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        SnapshotsList.ItemsSource = _vm.SnapshotInfos;
        RecoveryHintText.Text = _vm.RecoveryHint;
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _vm.RefreshCommand.Execute(null);
    private void OnGoHistoryClick(object sender, RoutedEventArgs e)
    {
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
        else if (sender is Button { Tag: Guid gid }) snapshotId = gid.ToString();
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

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await _vm.RevertCommand.ExecuteAsync(snapshotId);
            // Feedback explícito de éxito/fracaso
            var hint = _vm.RecoveryHint;
            var isSuccess = hint.Contains('✓') || hint.Contains("aceptada", StringComparison.OrdinalIgnoreCase);
            var resultDialog = new ContentDialog
            {
                Title = isSuccess ? "Restauración completada" : "Restauración no completada",
                Content = new TextBlock { Text = hint, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "Aceptar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            await resultDialog.ShowAsync();
            _vm.RefreshCommand.Execute(null);
            RenderVm();
        }
        catch (Exception ex)
        {
            RecoveryHintText.Text = $"Restauración falló (servicio no disponible): {ex.Message}";
            var errDialog = new ContentDialog
            {
                Title = "Error en restauración",
                Content = new TextBlock { Text = $"No se pudo revertir {snapshotId}:\n{ex.Message}\n\nVerifica que el servicio CAO.Privileged esté en ejecución.", TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "Aceptar",
                XamlRoot = Content.XamlRoot
            };
            await errDialog.ShowAsync();
        }
    }
}
