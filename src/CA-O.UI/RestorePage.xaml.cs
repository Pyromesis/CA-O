using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Shared;
using CAO.UI.Controls;
using CAO.UI.Helpers;

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
        _vm = AppHost.Resolve<ViewModels.RestoreViewModel>();
        DataContext = _vm;
        ApplyTexts();
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
        _vm.RefreshCommand.Execute(null);
        RecoveryHintText.Text = _vm.RecoveryHint;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.RestoreViewModel.Snapshots) or nameof(ViewModels.RestoreViewModel.RecoveryHint) or nameof(ViewModels.RestoreViewModel.IsEmpty) or nameof(ViewModels.RestoreViewModel.HasPendingRecoveries))
                DispatcherQueue.TryEnqueue(RenderVm);
        };
        RenderVm();
    }

    private void ApplyTexts()
    {
        NoteBar.Message = Localizer.Get("restore.pointNote");
        try { LocalizationHelper.LocalizeTree(this.Content as DependencyObject ?? this); } catch { }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyTexts();
        _vm.RefreshCommand.Execute(null);
    }

    private void RenderVm()
    {
        EmptySnapshotsCard.Visibility = _vm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        SnapshotsList.Visibility = _vm.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        SnapshotsList.ItemsSource = _vm.SnapshotInfos;
        RecoveryHintText.Text = _vm.RecoveryHint;
        // CAO-BUG-2026-10-06 (N-1): la tarjeta solo aparece si hay una operacion
        // pendiente y es la unica via para desbloquear el equipo cuando el
        // servicio rechaza aplicar y revertir.
        PendingRecoveryCard.Visibility = _vm.HasPendingRecoveries ? Visibility.Visible : Visibility.Collapsed;
        PendingRecoveryList.ItemsSource = _vm.PendingRecoveries;
    }

    /// <summary>
    /// Revierte una operacion pendiente desde su snapshot y cierra la transaccion.
    /// CAO-BUG-2026-10-06 (N-1): sin esta accion el equipo se quedaba bloqueado,
    /// porque el servicio devuelve <c>CAO-TXN-004</c> mientras haya recuperacion
    /// pendiente, tanto al aplicar como al revertir.
    /// </summary>
    private async void OnRecoverClick(object sender, RoutedEventArgs e) =>
        await RunRecoverAsync(sender, discardChanges: false);

    /// <summary>
    /// Cierra la transaccion aceptando el estado actual, sin revertir cambios.
    /// Es la unica salida cuando el snapshot no permite una reverticion fiable.
    /// </summary>
    private async void OnDiscardClick(object sender, RoutedEventArgs e) =>
        await RunRecoverAsync(sender, discardChanges: true);

    private async Task RunRecoverAsync(object sender, bool discardChanges)
    {
        if (sender is not Button { Tag: Guid transactionId }) return;

        var what = discardChanges
            ? "se descartará el registro y los cambios NO se revertirán"
            : "se revertirán los cambios desde el snapshot";
        var confirm = new ContentDialog
        {
            Title = discardChanges ? "Confirmar descarte" : "Confirmar recuperación",
            Content = new TextBlock
            {
                Text = $"Operación {transactionId:D}. Con esta acción {what}. Mientras quede una operación pendiente el servicio rechaza cualquier otro cambio.",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = discardChanges ? "Descartar" : "Recuperar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        if (await UiDialogs.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        Mascot.Set("Working");
        ShowRestoreProgress(discardChanges
            ? $"Descartando la operación {transactionId:D}…"
            : $"Revirtiendo la operación {transactionId:D}…");
        try
        {
            using var recoverCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await _vm.RecoverAsync(transactionId, discardChanges, recoverCts.Token);
            var hint = _vm.RecoveryHint;
            var isSuccess = hint.Contains('✓');
            ShowRestoreProgress(isSuccess ? hint : $"No se pudo recuperar: {hint}", done: true);
            if (isSuccess) Mascot.CelebrateThenIdle(DispatcherQueue);
            else Mascot.Set("Warn");
            var resultDialog = new ContentDialog
            {
                Title = isSuccess ? "Recuperación completada" : "Recuperación no completada",
                Content = new TextBlock { Text = hint, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "Aceptar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            await UiDialogs.ShowAsync(resultDialog);
            _vm.RefreshCommand.Execute(null);
            RenderVm();
            try { await Task.Delay(1500); } catch { }
            HideRestoreProgress();
        }
        catch (Exception ex)
        {
            RecoveryHintText.Text = $"Recuperación falló (servicio no disponible): {ex.Message}";
            Mascot.Set("Warn");
            ShowRestoreProgress($"Recuperación falló: {ex.Message}", done: true);
            var errDialog = new ContentDialog
            {
                Title = "Error en recuperación",
                Content = new TextBlock { Text = $"No se pudo cerrar la operación {transactionId:D}:\n{ex.Message}\n\nVerifica que el servicio CAO.Privileged esté en ejecución.", TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "Aceptar",
                XamlRoot = Content.XamlRoot
            };
            await UiDialogs.ShowAsync(errDialog);
            try { await Task.Delay(1500); } catch { }
            HideRestoreProgress();
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _vm.RefreshCommand.Execute(null);
    private void OnGoHistoryClick(object sender, RoutedEventArgs e)
    {
        // Botón funcional: navega al historial auditable en vez de solo
        // escribir una nota. Si la ruta no existe, se conserva el mensaje.
        if (MainWindow.Current is not null)
            MainWindow.Current.SelectRoute("history");
        else
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
        await UiDialogs.ShowAsync(dialog);
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
        if (await UiDialogs.ShowAsync(confirm) != ContentDialogResult.Primary) return;

        Mascot.Set("Working");
        ShowRestoreProgress($"Restaurando snapshot {snapshotId}…");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await _vm.RevertCommand.ExecuteAsync(snapshotId);
            // Feedback explícito de éxito/fracaso
            var hint = _vm.RecoveryHint;
            var isSuccess = hint.Contains('✓') || hint.Contains("aceptada", StringComparison.OrdinalIgnoreCase);
            ShowRestoreProgress(isSuccess ? $"✓ Snapshot {snapshotId} restaurado." : hint, done: true);
            if (isSuccess) Mascot.CelebrateThenIdle(DispatcherQueue);
            else Mascot.Set("Warn");
            var resultDialog = new ContentDialog
            {
                Title = isSuccess ? "Restauración completada" : "Restauración no completada",
                Content = new TextBlock { Text = hint, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "Aceptar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            await UiDialogs.ShowAsync(resultDialog);
            _vm.RefreshCommand.Execute(null);
            RenderVm();
            try { await Task.Delay(1500); } catch { }
            HideRestoreProgress();
        }
        catch (Exception ex)
        {
            RecoveryHintText.Text = $"Restauración falló (servicio no disponible): {ex.Message}";
            Mascot.Set("Warn");
            ShowRestoreProgress($"Restauración falló: {ex.Message}", done: true);
            var errDialog = new ContentDialog
            {
                Title = "Error en restauración",
                Content = new TextBlock { Text = $"No se pudo revertir {snapshotId}:\n{ex.Message}\n\nVerifica que el servicio CAO.Privileged esté en ejecución.", TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "Aceptar",
                XamlRoot = Content.XamlRoot
            };
            await UiDialogs.ShowAsync(errDialog);
            try { await Task.Delay(1500); } catch { }
            HideRestoreProgress();
        }
    }

    /// <summary>Muestra la tarjeta global de progreso. Nunca lanza.</summary>
    private void ShowRestoreProgress(string text, bool done = false)
    {
        try
        {
            RestoreProgressCard.Visibility = Visibility.Visible;
            RestoreRing.IsActive = !done;
            RestoreProgressText.Text = text;
            RestoreProgressBar.IsIndeterminate = !done;
            if (done) RestoreProgressBar.Value = 100;
        }
        catch { }
    }

    private void HideRestoreProgress()
    {
        try { RestoreProgressCard.Visibility = Visibility.Collapsed; RestoreRing.IsActive = false; } catch { }
    }
}
