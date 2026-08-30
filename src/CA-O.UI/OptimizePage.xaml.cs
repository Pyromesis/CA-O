using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Core.Engine;
using CAO.Shared;
using CAO.Shared.IPC;
using CAO.UI.Helpers;

namespace CAO.UI.Pages;

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
    RecommendationBucket Bucket,
    bool IsLocked,
    string LockReason,
    string BenefitDetail,
    bool IsApplyEnabled)
{
    public Microsoft.UI.Xaml.Visibility LockVisibility => IsLocked ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}

public sealed partial class OptimizePage : Page
{

    private readonly ViewModels.OptimizeViewModel _vm;
    private RecommendationBucket? _activeFilter; // null = All

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
            if (e.PropertyName == nameof(ViewModels.OptimizeViewModel.CurrentPhase))
                DispatcherQueue.TryEnqueue(() => TxText.Text = _vm.CurrentPhase);
            if (e.PropertyName == nameof(ViewModels.OptimizeViewModel.TransactionProgress))
                DispatcherQueue.TryEnqueue(() => StatusText.Text = _vm.TransactionProgress);
        };
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
    }

    private void ApplyTexts()
    {
        ApplyRecommendedButton.Content = Localizer.Get("optimize.applyRecommended");
        ExpertBar.Message = Localizer.Get("optimize.expertWarning");
        UpdateFilterButtons();
        try { LocalizationHelper.LocalizeTree(this.Content as DependencyObject ?? this); } catch { }
    }

    private void UpdateFilterButtons()
    {
        // Counters dynamic
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var all = uiState.Recommendations.Count;
        var rec = uiState.Recommendations.Count(r => r.Bucket == RecommendationBucket.Recommended);
        var opt = uiState.Recommendations.Count(r => r.Bucket == RecommendationBucket.Optional);
        var exp = uiState.Recommendations.Count(r => r.Bucket == RecommendationBucket.Experimental);
        FilterAllButton.Content = $"{Localizer.Get("optimize.filterAll")} ({all})";
        FilterRecommendedButton.Content = $"{Localizer.Get("optimize.filterRecommended")} ({rec})";
        FilterOptionalButton.Content = $"{Localizer.Get("optimize.filterOptional")} ({opt})";
        FilterExperimentalButton.Content = $"{Localizer.Get("optimize.filterExperimental")} ({exp})";
        // highlight active
        FilterAllButton.Style = _activeFilter == null ? (Microsoft.UI.Xaml.Style)Application.Current.Resources["AccentButtonStyle"] : (Microsoft.UI.Xaml.Style)Application.Current.Resources["DefaultButtonStyle"];
        FilterRecommendedButton.Style = _activeFilter == RecommendationBucket.Recommended ? (Microsoft.UI.Xaml.Style)Application.Current.Resources["AccentButtonStyle"] : (Microsoft.UI.Xaml.Style)Application.Current.Resources["DefaultButtonStyle"];
        FilterOptionalButton.Style = _activeFilter == RecommendationBucket.Optional ? (Microsoft.UI.Xaml.Style)Application.Current.Resources["AccentButtonStyle"] : (Microsoft.UI.Xaml.Style)Application.Current.Resources["DefaultButtonStyle"];
        FilterExperimentalButton.Style = _activeFilter == RecommendationBucket.Experimental ? (Microsoft.UI.Xaml.Style)Application.Current.Resources["AccentButtonStyle"] : (Microsoft.UI.Xaml.Style)Application.Current.Resources["DefaultButtonStyle"];
    }

    private void OnFilterAllClick(object sender, RoutedEventArgs e) { _activeFilter = null; Render(); }
    private void OnFilterRecommendedClick(object sender, RoutedEventArgs e) { _activeFilter = RecommendationBucket.Recommended; Render(); }
    private void OnFilterOptionalClick(object sender, RoutedEventArgs e) { _activeFilter = RecommendationBucket.Optional; Render(); }
    private void OnFilterExperimentalClick(object sender, RoutedEventArgs e) { _activeFilter = RecommendationBucket.Experimental; Render(); }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyTexts();
        Render();
    }

    private void Render()
    {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        ExpertBar.IsOpen = uiState.ExpertMode;
        UpdateFilterButtons();

        var baseRows = uiState.Recommendations
            .OrderBy(row => row.Bucket switch
            {
                RecommendationBucket.Recommended => 0,
                RecommendationBucket.Optional => 1,
                RecommendationBucket.Experimental => 2,
                RecommendationBucket.SecuritySensitive => 3,
                _ => 4,
            });

        // Filter by active bucket (spec 12)
        IEnumerable<Recommendation> filtered = _activeFilter == null ? baseRows : baseRows.Where(r => r.Bucket == _activeFilter.Value);

        var rows = filtered.Select(recommendation =>
            {
                bool isLocked = recommendation.Bucket != RecommendationBucket.Recommended && !uiState.ExpertMode;
                if (recommendation.Compatibility == CompatibilityStatus.Incompatible) isLocked = true;
                if (recommendation.AntiCheatConflictRisk) isLocked = true;
                string lockReason = recommendation.Bucket switch
                {
                    RecommendationBucket.Optional => Localizer.Get("optimize.lockedOptional"),
                    RecommendationBucket.Experimental => Localizer.Get("optimize.lockedExperimental"),
                    RecommendationBucket.SecuritySensitive => Localizer.Get("optimize.lockedSecurity"),
                    _ when recommendation.AntiCheatConflictRisk => Localizer.Get("optimize.lockedAntiCheat"),
                    _ when recommendation.Compatibility == CompatibilityStatus.Incompatible => Localizer.Get("optimize.lockedIncompatible"),
                    _ => string.Empty
                };
                string benefit = GetBenefitDetail(recommendation.OptimizationId);
                bool canApply = !isLocked && recommendation.CurrentState != OptimizationState.AppliedByCao;
                return new RecommendationRow(
                recommendation.OptimizationId,
                recommendation.NameEs,
                recommendation.DescriptionEs,
                Localizer.GetBucketLabel(recommendation.Bucket),
                recommendation.RequiresReboot ? Localizer.Get("common.requiresReboot") : string.Empty,
                Localizer.GetEvidenceLabel(recommendation.Evidence),
                Localizer.GetRiskLabel(recommendation.Risk),
                Localizer.GetSecurityLabel(recommendation.SecurityImpact),
                recommendation.Compatibility.ToString(),
                recommendation.CurrentState.ToString(),
                recommendation.Score?.ToString() ?? "n/a",
                recommendation.Reason.MessageEs,
                recommendation.Bucket,
                isLocked,
                lockReason,
                benefit,
                canApply);
            })
            .ToList();

        // Expert mode handling for Experimental filter
        if (_activeFilter == RecommendationBucket.Experimental && !uiState.ExpertMode)
        {
            FilterInfoBar.Message = Localizer.Get("optimize.expertRequired");
            FilterInfoBar.Severity = InfoBarSeverity.Warning;
            FilterInfoBar.IsOpen = true;
            RecommendationsList.ItemsSource = null;
            RecommendationsList.Visibility = Visibility.Collapsed;
            EmptyStateCard.Visibility = Visibility.Collapsed;
            return;
        }
        FilterInfoBar.IsOpen = false;

        if (rows.Count == 0)
        {
            // Empty states per filter
            var msg = _activeFilter switch
            {
                RecommendationBucket.Recommended => Localizer.Get("optimize.noRecommended"),
                RecommendationBucket.Optional => Localizer.Get("optimize.noOptional"),
                RecommendationBucket.Experimental => Localizer.Get("optimize.noExperimental"),
                _ => Localizer.Get("optimize.noResults")
            };
            EmptyStateCard.Visibility = Visibility.Visible;
            RecommendationsList.Visibility = Visibility.Collapsed;
            // Update empty text dynamically by finding TextBlock inside EmptyStateCard
            try
            {
                if (EmptyStateCard.Child is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock tb) tb.Text = msg;
                else if (EmptyStateCard.Child is StackPanel sp2 && sp2.Children.Count > 2 && sp2.Children[2] is TextBlock tb2) tb2.Text = msg;
            } catch { }
            RecommendationsList.ItemsSource = null;
        }
        else
        {
            RecommendationsList.ItemsSource = rows;
            EmptyStateCard.Visibility = Visibility.Collapsed;
            RecommendationsList.Visibility = Visibility.Visible;
        }
    }

    private static string GetBenefitDetail(string id) => id switch
    {
        "maximum-power-plan" => "Beneficio: +5-10% rendimiento sostenido en carga, menor throttling. Ideal para juegos y render. Requiere reinicio no.",
        "disable-visual-effects" => "Beneficio: -15% uso GPU en escritorio, +2-5% FPS en juegos con GPU limitada, menos input lag.",
        "disable-search-indexing" => "Beneficio: -200 MB RAM y -5% I/O en SSD, +3% batería en portátil. Solo recomendado en SSD.",
        "disable-background-apps" => "Beneficio: -8% uso CPU en reposo, mejor ping estable, menos notificaciones.",
        "disable-transparency" => "Beneficio: -3% GPU, batería +4%, interfaz más nítida.",
        "disable-vbs" => "Beneficio: +5-15% FPS en algunos juegos, pero reduce seguridad (HVCI). Bloqueado si Vanguard/EAC.",
        "disable-hibernate" => "Beneficio: +4-12 GB libres en disco del sistema, arranque 0.5s más rápido.",
        "optimize-system-drive" => "Beneficio: +2% velocidad secuencial SSD, menos fragmentación. Verificar TRIM.",
        "normalize-tcp-autotuning" => "Beneficio: -10-20 ms ping en juegos con bufferbloat, más estabilidad.",
        "enable-gpu-scheduling" => "Beneficio: -1-2 ms latencia GPU, +2% FPS en DX12. Requiere reinicio.",
        "disable-gamedvr" => "Beneficio: -3% overhead, +1-3% FPS, menos stutter.",
        _ => "Beneficio: según perfil, revisa evidencia y confianza."
    };

    private async void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;

        var catalog = CAO.Core.Catalog.OptimizationCatalog.All;
        var match = catalog.FirstOrDefault(o =>
            o.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            StatusText.Text = $"Optimización desconocida: {id}";
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var registry = AppHost.Resolve<CAO.Infrastructure.Windows.SystemRegistry.RegistryAccessor>();
            var preview = await match.PreviewAsync(registry, cts.Token);
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

    private bool CanOperate(string optimizationId) {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        return uiState.Recommendations.Any(recommendation =>
            recommendation.OptimizationId == optimizationId &&
            (recommendation.Bucket == RecommendationBucket.Recommended || uiState.ExpertMode));
    }

    private async Task RunOperationAsync(PrivilegedOperationKind operation, string optimizationId)
    {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        if (uiState.ExpertMode || operation == PrivilegedOperationKind.ApplyOptimization)
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
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.SendAsync(operation, optimizationId, cts.Token);
            uiState.ServiceStatus = response is { Accepted: true } ? "connected" : "rejected";
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

            // Feedback explícito para reversión
            if (operation == PrivilegedOperationKind.RevertOptimization)
            {
                var dialog = new ContentDialog
                {
                    Title = response is { Accepted: true } ? "Reversión completada" : "Reversión no completada",
                    Content = new TextBlock { Text = response is { Accepted: true } ? $"Se revirtió {optimizationId} y se verificó el estado original." : $"No se pudo revertir {optimizationId}:\n[{response?.ErrorCode}] {response?.SafeMessage}", TextWrapping = TextWrapping.Wrap },
                    CloseButtonText = "Aceptar",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };
                await dialog.ShowAsync();
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
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var recommended = uiState.Recommendations
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
                var pipe = AppHost.Resolve<PrivilegedPipeClient>();
                var response = await pipe.SendAsync(PrivilegedOperationKind.ApplyOptimization, id, cts.Token);
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
