using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Core.Engine;
using CAO.Shared;
using CAO.Shared.IPC;

namespace CAO.UI.Pages;

/// <summary>
/// Optimization cards (spec 79): name, description, current vs target state,
/// evidence, risk, security and anti-cheat impact, reboot requirement,
/// rollback availability. Apply is restricted to Recommended unless Expert
/// mode is on, and every apply travels through the privileged service.
/// </summary>
public sealed partial class OptimizePage : Page
{
    /// <summary>Row view-model binding the card template.</summary>
    public sealed record RecommendationRow(
        string OptimizationId,
        string NameEs,
        string DescriptionEs,
        string BucketLabel,
        string RebootLabel,
        string Evidence,
        string Risk,
        string SecurityImpact,
        string Compatibility,
        string CurrentState,
        string ScoreLabel,
        string ReasonMessage,
        RecommendationBucket Bucket);

    private readonly ViewModels.OptimizeViewModel _vm;

    public OptimizePage()
    {
        InitializeComponent();
        ApplyTexts();
        _vm = AppHost.Resolve<ViewModels.OptimizeViewModel>();
        DataContext = _vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModels.OptimizeViewModel.IsBusy))
                DispatcherQueue.TryEnqueue(() => BusyRing.IsActive = _vm.IsBusy);
        };
    }

    private void ApplyTexts()
    {
        ApplyRecommendedButton.Content = Localizer.Get("optimize.applyRecommended");
        ExpertBar.Message = Localizer.Get("optimize.expertWarning");
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Render();
    }

    private void Render()
    {
        ExpertBar.IsOpen = AppServices.State.ExpertMode;

        var rows = AppServices.State.Recommendations
            .OrderBy(row => row.Bucket switch
            {
                RecommendationBucket.Recommended => 0,
                RecommendationBucket.Optional => 1,
                RecommendationBucket.Experimental => 2,
                RecommendationBucket.SecuritySensitive => 3,
                _ => 4,
            })
            .Select(recommendation => new RecommendationRow(
                recommendation.OptimizationId,
                recommendation.NameEs,
                recommendation.DescriptionEs,
                recommendation.Bucket.ToString(),
                recommendation.RequiresReboot ? "requiere reinicio" : string.Empty,
                recommendation.Evidence.ToString(),
                recommendation.Risk.ToString(),
                recommendation.SecurityImpact.ToString(),
                recommendation.Compatibility.ToString(),
                recommendation.CurrentState.ToString(),
                recommendation.Score?.ToString() ?? "n/a",
                recommendation.Reason.MessageEs,
                recommendation.Bucket))
            .ToList();

        RecommendationsList.ItemsSource = rows;
        EmptyStateCard.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecommendationsList.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;

        var match = AppServices.Catalog.FirstOrDefault(o =>
            o.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            StatusText.Text = $"Optimización desconocida: {id}";
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var preview = await match.PreviewAsync(AppServices.Registry, cts.Token);
            // Real diff view (Fase 12): Before/After per target, not generic text.
            var diffPanel = new StackPanel { Spacing = 10 };
            foreach (var line in preview.Lines)
            {
                var card = new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 4, 0, 0),
                };
                var inner = new StackPanel { Spacing = 4 };
                inner.Children.Add(new TextBlock { Text = $"{line.Kind}  ·  {line.Target}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 });
                var grid = new Grid { ColumnSpacing = 8 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var beforeBox = new Border { Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"], CornerRadius = new CornerRadius(6), Padding = new Thickness(8) };
                beforeBox.Child = new StackPanel { Children = { new TextBlock { Text = "ANTES", FontSize = 10, Opacity = 0.6 }, new TextBlock { Text = line.Before, TextWrapping = TextWrapping.Wrap, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 11, IsTextSelectionEnabled = true } } };
                var afterBox = new Border { Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"], Opacity = 0.15 };
                afterBox.CornerRadius = new CornerRadius(6); afterBox.Padding = new Thickness(8);
                afterBox.Child = new StackPanel { Children = { new TextBlock { Text = "DESPUÉS", FontSize = 10, Opacity = 0.8 }, new TextBlock { Text = line.After, TextWrapping = TextWrapping.Wrap, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 11, IsTextSelectionEnabled = true } } };
                Grid.SetColumn(beforeBox, 0); Grid.SetColumn(afterBox, 1);
                grid.Children.Add(beforeBox); grid.Children.Add(afterBox);
                inner.Children.Add(grid);
                card.Child = inner;
                diffPanel.Children.Add(card);
            }
            diffPanel.Children.Add(new TextBlock
            {
                Text = $"Riesgo: {preview.Risk} · Seguridad: {preview.SecurityImpact} · Reversible: {(preview.Reversible ? "sí" : "NO — irreversible aun con snapshot")} · Reinicio: {(preview.RequiresReboot ? "sí" : "no")}",
                FontSize = 11, Opacity = 0.7, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
            });

            var dialog = new ContentDialog
            {
                Title = $"Vista previa — {preview.OptimizationId}",
                Content = new ScrollViewer { MaxHeight = 460, Content = diffPanel },
                CloseButtonText = "Cerrar",
                PrimaryButtonText = "Aplicar este cambio",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await RunOperationAsync(PrivilegedOperationKind.ApplyOptimization, id);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"El dry-run falló: {ex.Message}";
        }
    }

    private async void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        if (!CanOperate(id))
        {
            StatusText.Text = "Sólo se aplican cambios Recomendados (o Expert con confirmación).";
            return;
        }

        await RunOperationAsync(PrivilegedOperationKind.ApplyOptimization, id);
    }

    private async void OnRevertClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        await RunOperationAsync(PrivilegedOperationKind.RevertOptimization, id);
    }

    private bool CanOperate(string optimizationId) =>
        AppServices.State.Recommendations.Any(recommendation =>
            recommendation.OptimizationId == optimizationId &&
            (recommendation.Bucket == RecommendationBucket.Recommended || AppServices.State.ExpertMode));

    private async Task RunOperationAsync(PrivilegedOperationKind operation, string optimizationId)
    {
        if (AppServices.State.ExpertMode || operation == PrivilegedOperationKind.ApplyOptimization)
        {
            var dialog = new ContentDialog
            {
                Title = "Confirmar operación",
                Content = $"Se ejecutará '{operation}' sobre '{optimizationId}'. Se creará un snapshot previo por TransactionId, se verificará exactamente y quedará reversible.",
                PrimaryButtonText = "Continuar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        BusyRing.IsActive = true;
        TransactionProgressCard.Visibility = Visibility.Visible;
        TxText.Text = operation == PrivilegedOperationKind.ApplyOptimization ? "Aplicando cambio transaccional…" : "Revirtiendo…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await AppServices.Pipe.SendAsync(operation, optimizationId, cts.Token);
            AppServices.State.ServiceStatus = response is { Accepted: true } ? "connected" : "rejected";
            if (response is { Accepted: true })
            {
                StatusText.Text = operation == PrivilegedOperationKind.ApplyOptimization ? "✓ Aplicado y verificado. Snapshot disponible para reversión." : "✓ Revertido y verificado.";
                TxText.Text = "Verificado ✓ — Commit OK";
                if (operation == PrivilegedOperationKind.ApplyOptimization) StatusText.Text += $" [{_vm.LastErrorCode ?? ""}]";
            }
            else
            {
                StatusText.Text = $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta del servicio"}";
                TxText.Text = "Rechazado — transacción no comprometida";
            }

            using var refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _vm.RefreshRecommendationsAsync(refreshCts.Token);
            Render();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Operación cancelada.";
            TxText.Text = "Cancelado";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Servicio no disponible: {ex.Message} (CAO-IPC-004 — verifique que CA-O Service esté instalado)";
            TxText.Text = "Servicio no disponible";
            App.WriteCrashLog(ex);
        }
        finally
        {
            BusyRing.IsActive = false;
            await Task.Delay(1200);
            TransactionProgressCard.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnApplyRecommendedClick(object sender, RoutedEventArgs e)
    {
        var recommended = AppServices.State.Recommendations
            .Where(recommendation => recommendation.Bucket == RecommendationBucket.Recommended &&
                                     recommendation.CurrentState != OptimizationState.AppliedByCao)
            .Select(recommendation => recommendation.OptimizationId)
            .ToList();

        if (recommended.Count == 0)
        {
            StatusText.Text = "No hay recomendaciones pendientes; ejecute el análisis primero.";
            return;
        }

        BusyRing.IsActive = true;
        var failures = new List<string>();
        try
        {
            foreach (var id in recommended)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await AppServices.Pipe.SendAsync(PrivilegedOperationKind.ApplyOptimization, id, cts.Token);
                if (response is not { Accepted: true })
                {
                    failures.Add($"{id}: [{response?.ErrorCode}] {response?.SafeMessage ?? "sin respuesta"}");
                    break; // stop the batch on first failure (spec 124)
                }
            }

            using var refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _vm.RefreshRecommendationsAsync(refreshCts.Token);
            Render();
            StatusText.Text = failures.Count == 0
                ? $"Aplicados {recommended.Count} cambios recomendados."
                : $"Lote detenido: {string.Join("; ", failures)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Servicio no disponible: {ex.Message}";
            App.WriteCrashLog(ex);
        }
        finally
        {
            BusyRing.IsActive = false;
        }
    }
}
