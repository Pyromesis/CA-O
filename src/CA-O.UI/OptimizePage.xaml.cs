using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Text;
using CAO.Core.Engine;
using CAO.Shared;
using CAO.Shared.IPC;
using CAO.UI.Controls;
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
    bool IsApplyEnabled,
    bool IsApplied,
    string ApplyLabel,
    string TooltipDetail,
    string MeasureLabel)
{
    public Microsoft.UI.Xaml.Visibility LockVisibility => IsLocked ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility AppliedVisibility => IsApplied ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}

public sealed partial class OptimizePage : Page
{

    private readonly ViewModels.OptimizeViewModel _vm;
    private RecommendationBucket? _activeFilter; // null = All
    private bool _appliedOnly; // filtro "Activos"

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
        var applied = uiState.Recommendations.Count(r => r.CurrentState == OptimizationState.AppliedByCao);
        FilterAllButton.Content = $"{Localizer.Get("optimize.filterAll")} ({all})";
        FilterRecommendedButton.Content = $"{Localizer.Get("optimize.filterRecommended")} ({rec})";
        FilterOptionalButton.Content = $"{Localizer.Get("optimize.filterOptional")} ({opt})";
        FilterExperimentalButton.Content = $"{Localizer.Get("optimize.filterExperimental")} ({exp})";
        FilterAppliedButton.Content = $"{Localizer.Get("optimize.filterApplied")} ({applied})";
        // highlight active (estilos premium Cao; el activo en acento, el resto filtro estable)
        var activeStyle = (Microsoft.UI.Xaml.Style)Application.Current.Resources["CaoAccentButtonStyle"];
        var idleStyle = (Microsoft.UI.Xaml.Style)Application.Current.Resources["CaoFilterButtonStyle"];
        FilterAllButton.Style = _activeFilter == null && !_appliedOnly ? activeStyle : idleStyle;
        FilterRecommendedButton.Style = _activeFilter == RecommendationBucket.Recommended && !_appliedOnly ? activeStyle : idleStyle;
        FilterOptionalButton.Style = _activeFilter == RecommendationBucket.Optional && !_appliedOnly ? activeStyle : idleStyle;
        FilterExperimentalButton.Style = _activeFilter == RecommendationBucket.Experimental && !_appliedOnly ? activeStyle : idleStyle;
        FilterAppliedButton.Style = _appliedOnly ? activeStyle : idleStyle;
    }

    private void OnFilterAllClick(object sender, RoutedEventArgs e) { _activeFilter = null; _appliedOnly = false; Render(); }
    private void OnFilterRecommendedClick(object sender, RoutedEventArgs e) { _activeFilter = RecommendationBucket.Recommended; _appliedOnly = false; Render(); }
    private void OnFilterOptionalClick(object sender, RoutedEventArgs e) { _activeFilter = RecommendationBucket.Optional; _appliedOnly = false; Render(); }
    private void OnFilterExperimentalClick(object sender, RoutedEventArgs e) { _activeFilter = RecommendationBucket.Experimental; _appliedOnly = false; Render(); }
    private void OnFilterAppliedClick(object sender, RoutedEventArgs e) { _activeFilter = null; _appliedOnly = true; Render(); }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyTexts();
        Render();
        CAO.UI.Helpers.UiAnimations.PlayEntrance(PageContent);
    }

    private void Render()
    {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        ExpertBar.IsOpen = uiState.ExpertMode;
        UpdateFilterButtons();

        // Los ya aplicados van siempre abajo; dentro, por bucket.
        var baseRows = uiState.Recommendations
            .OrderBy(row => row.CurrentState == OptimizationState.AppliedByCao ? 1 : 0)
            .ThenBy(row => row.Bucket switch
            {
                RecommendationBucket.Recommended => 0,
                RecommendationBucket.Optional => 1,
                RecommendationBucket.Experimental => 2,
                RecommendationBucket.SecuritySensitive => 3,
                _ => 4,
            });

        // Filter by active bucket (spec 12) or applied-only view
        IEnumerable<Recommendation> filtered = baseRows;
        if (_appliedOnly) filtered = filtered.Where(r => r.CurrentState == OptimizationState.AppliedByCao);
        else if (_activeFilter != null) filtered = filtered.Where(r => r.Bucket == _activeFilter.Value);

        var rows = filtered.Select(recommendation =>
            {
                var (isLocked, lockReason) = EvaluateLock(recommendation, uiState.ExpertMode, uiState.Recommendations);
                string benefit = GetBenefitDetail(recommendation.OptimizationId);
                bool isApplied = recommendation.CurrentState == OptimizationState.AppliedByCao
                    || uiState.AppliedThisSession.Contains(recommendation.OptimizationId);
                bool canApply = !isLocked && !isApplied;
                string tooltip = BuildTooltip(recommendation);
                return new RecommendationRow(
                recommendation.OptimizationId,
                recommendation.NameEs,
                recommendation.DescriptionEs,
                Localizer.GetBucketLabel(recommendation.Bucket),
                recommendation.RequiresReboot ? Localizer.Get("common.requiresReboot") : string.Empty,
                Localizer.GetEvidenceLabel(recommendation.Evidence),
                Localizer.GetRiskLabel(recommendation.Risk),
                Localizer.GetSecurityLabel(recommendation.SecurityImpact),
                Localizer.GetCompatibilityLabel(recommendation.Compatibility),
                Localizer.GetStateLabel(recommendation.CurrentState),
                recommendation.Score?.ToString() ?? "n/a",
                recommendation.Reason.MessageEs,
                recommendation.Bucket,
                isLocked,
                lockReason,
                benefit,
                canApply,
                isApplied,
                isApplied ? "Aplicado ✓" : "Aplicar",
                tooltip,
                Localizer.Get("benchmark.measureOpt"));
            })
            .ToList();

        // Expert mode handling for Experimental filter (solo fuera de la vista Activos)
        if (!_appliedOnly && _activeFilter == RecommendationBucket.Experimental && !uiState.ExpertMode)
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
            var msg = _appliedOnly ? Localizer.Get("optimize.noApplied")
                : _activeFilter switch
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

    private static string BuildTooltip(Recommendation recommendation)
    {
        var def = CAO.Core.Catalog.OptimizationCatalog.All
            .FirstOrDefault(o => o.Definition.Id.Equals(recommendation.OptimizationId, StringComparison.OrdinalIgnoreCase))?.Definition;
        var sb = new System.Text.StringBuilder(recommendation.DescriptionEs);
        if (def is not null && !string.IsNullOrWhiteSpace(def.TooltipEs) && !def.TooltipEs.Equals(recommendation.DescriptionEs, StringComparison.OrdinalIgnoreCase))
        {
            sb.Append('\n');
            sb.Append(def.TooltipEs);
        }
        sb.Append($"\nRiesgo: {Localizer.GetRiskLabel(recommendation.Risk)}");
        sb.Append($"\nReversible: {(def?.Reversible == true ? "sí — snapshot y Revertir" : "no — mantenimiento")}");
        if (recommendation.RequiresReboot) sb.Append("\nRequiere reinicio para efecto completo.");
        return sb.ToString();
    }

    private static string GetBenefitDetail(string id) => id switch
    {
        "maximum-power-plan" => "Beneficio: rendimiento sostenido en carga al evitar planes de ahorro. Ideal para juegos y render.",
        "disable-visual-effects" => "Beneficio: menos carga de GPU en escritorio y respuesta más ágil en equipos modestos.",
        "disable-search-indexing" => "Beneficio: menos RAM e I/O en segundo plano. Solo recomendado en SSD.",
        "disable-background-apps" => "Beneficio: menos CPU en reposo y menos interrupciones.",
        "disable-transparency" => "Beneficio: menos carga de composición y algo más de batería.",
        "disable-vbs" => "Beneficio: puede mejorar en algunos juegos, pero reduce seguridad (HVCI). Bloqueado si Vanguard/EAC.",
        "disable-hibernate" => "Beneficio: libera el espacio de hiberfil.sys en el disco del sistema.",
        "optimize-system-drive" => "Beneficio: unidad optimizada según su medio (TRIM en SSD). Requiere minutos.",
        "normalize-tcp-autotuning" => "Beneficio: comportamiento de red estándar y estable.",
        "enable-gpu-scheduling" => "Beneficio: planificación GPU por hardware. Requiere reinicio.",
        "disable-gamedvr" => "Beneficio: menos sobrecarga al jugar si no grabas clips.",
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
            // El diálogo respeta los mismos bloqueos que la tarjeta: si está bloqueado
            // o ya aplicado, no se ofrece "Aplicar este cambio".
            var uiStatePreview = AppHost.Resolve<ViewModels.UiState>();
            var previewRec = uiStatePreview.Recommendations.FirstOrDefault(r =>
                r.OptimizationId.Equals(id, StringComparison.OrdinalIgnoreCase));
            bool previewLocked = true;
            string previewLockReason = "Sin análisis vigente: ejecute Analizar primero.";
            bool previewApplied = false;
            if (previewRec is not null)
            {
                (previewLocked, previewLockReason) = EvaluateLock(previewRec, uiStatePreview.ExpertMode, uiStatePreview.Recommendations);
                previewApplied = previewRec.CurrentState == OptimizationState.AppliedByCao
                    || uiStatePreview.AppliedThisSession.Contains(id);
            }
            // Real diff view (Fase 12): Before/After per target, not generic text.
            var diffPanel = new StackPanel { Spacing = 10 };
            if (previewLocked || previewApplied)
            {
                diffPanel.Children.Add(new InfoBar
                {
                    Severity = previewApplied && !previewLocked ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                    Title = previewApplied && !previewLocked ? "Ya aplicado" : "Bloqueado",
                    Message = previewApplied && !previewLocked
                        ? "CA-O ya lo aplicó — no se puede volver a aplicar. Use Revertir si desea restaurarlo."
                        : $"No se puede aplicar: {previewLockReason}",
                    IsOpen = true,
                    IsClosable = false,
                    Margin = new Thickness(0, 0, 0, 4),
                });
            }
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
                inner.Children.Add(new TextBlock { Text = $"{Localizer.GetDiffKindLabel(line.Kind)}  ·  {line.Target}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 });
                var grid = new Grid { ColumnSpacing = 8 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var beforeBox = new Border { Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"], CornerRadius = new CornerRadius(6), Padding = new Thickness(8) };
                beforeBox.Child = new StackPanel { Children = { new TextBlock { Text = "ANTES", FontSize = 10, Opacity = 0.6 }, new TextBlock { Text = line.Before, TextWrapping = TextWrapping.Wrap, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 11, IsTextSelectionEnabled = true } } };
                var afterBox = new Border { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 108, 187, 89)) };
                afterBox.CornerRadius = new CornerRadius(6); afterBox.Padding = new Thickness(8);
                afterBox.Child = new StackPanel { Children = { new TextBlock { Text = "DESPUÉS", FontSize = 10, Opacity = 0.7 }, new TextBlock { Text = line.After, TextWrapping = TextWrapping.Wrap, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 11, IsTextSelectionEnabled = true } } };
                Grid.SetColumn(beforeBox, 0); Grid.SetColumn(afterBox, 1);
                grid.Children.Add(beforeBox); grid.Children.Add(afterBox);
                inner.Children.Add(grid);
                card.Child = inner;
                diffPanel.Children.Add(card);
            }
            diffPanel.Children.Add(new InfoBar
            {
                Severity = InfoBarSeverity.Informational,
                Title = "Datos del cambio",
                Message = $"Riesgo: {Localizer.GetRiskLabel(preview.Risk)} · Seguridad: {Localizer.GetSecurityLabel(preview.SecurityImpact)} · Reversible: {(preview.Reversible ? "sí" : "NO — irreversible aun con snapshot")} · Reinicio: {(preview.RequiresReboot ? "sí" : "no")}",
                IsOpen = true,
                IsClosable = false,
                Margin = new Thickness(0, 8, 0, 0),
            });

            bool offerApply = !previewLocked && !previewApplied;
            bool offerRevert = !previewLocked && previewApplied;
            string previewName = previewRec?.NameEs ?? preview.OptimizationId;
            string previewDesc = previewRec?.DescriptionEs ?? string.Empty;
            var previewRoot = new StackPanel { Spacing = 10 };
            previewRoot.Children.Add(new Controls.CaoCat { Width = 96, Height = 96, HorizontalAlignment = HorizontalAlignment.Center });
            previewRoot.Children.Add(new TextBlock { Text = offerRevert ? "¿Qué se va a restaurar?" : "¿Qué va a cambiar?", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            previewRoot.Children.Add(new TextBlock { Text = previewName, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrWhiteSpace(previewDesc))
                previewRoot.Children.Add(new TextBlock { Text = previewDesc, FontSize = 12, Opacity = 0.8, TextWrapping = TextWrapping.Wrap });
            previewRoot.Children.Add(diffPanel);
            var dialog = new ContentDialog
            {
                Title = $"Vista previa — {previewName}",
                Content = new ScrollViewer { MaxHeight = 460, Content = previewRoot },
                CloseButtonText = "Cerrar",
                DefaultButton = (offerApply || offerRevert) ? ContentDialogButton.Primary : ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (offerApply) dialog.PrimaryButtonText = "Sí, aplicar";
            else if (offerRevert) dialog.PrimaryButtonText = "Sí, revertir";
            if (offerApply || offerRevert)
                dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"];
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (offerRevert) await RunOperationAsync(PrivilegedOperationKind.RevertOptimization, id);
                else if (offerApply) await RunOperationAsync(PrivilegedOperationKind.ApplyOptimization, id);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"El dry-run falló: {ex.Message}";
        }
    }

    private void OnMeasureImpactClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var recommendation = uiState.Recommendations.FirstOrDefault(r =>
            r.OptimizationId.Equals(id, StringComparison.OrdinalIgnoreCase));
        uiState.PendingBenchmarkOptimizationId = id;
        uiState.PendingBenchmarkCategory = recommendation?.Category.ToString() ?? string.Empty;
        if (MainWindow.Current is not null) MainWindow.Current.SelectRoute("benchmark");
        else AppHost.Resolve<Navigation.INavigationService>().Select("benchmark");
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

    /// <summary>
    /// Única regla de bloqueo: bucket no-Recommended sin Modo Expert, hardware
    /// incompatible, conflicto anti-cheat, plan de energía en conflicto con el
    /// activo, cambio ya aplicado o auditoría de solo diagnóstico. La comparten
    /// la tarjeta, el diálogo de Detalles y el guard.
    /// </summary>
    private static (bool IsLocked, string LockReason) EvaluateLock(
        Recommendation recommendation, bool expertMode,
        IReadOnlyList<Recommendation>? all = null)
    {
        bool isLocked = recommendation.Bucket != RecommendationBucket.Recommended && !expertMode;
        if (recommendation.Compatibility == CompatibilityStatus.Incompatible) isLocked = true;
        if (recommendation.AntiCheatConflictRisk) isLocked = true;
        if (recommendation.ExpectedImpact == PerformanceImpact.DiagnosticOnly) isLocked = true;
        string? conflictSibling = null;
        if (all is not null)
        {
            conflictSibling = CAO.Core.Optimization.OptimizationConflicts.FindAppliedSibling(
                recommendation.OptimizationId,
                all.Select(r => (r.OptimizationId, r.CurrentState)));
        }
        if (conflictSibling is not null) isLocked = true;
        string lockReason = conflictSibling is not null
            ? $"En conflicto: '{conflictSibling}' ya está activo. Reviertelo antes de activar este plan: dos planes no pueden estar activos a la vez."
            : recommendation.ExpectedImpact == PerformanceImpact.DiagnosticOnly
            ? Localizer.Get("optimize.lockedDiagnostic")
            : recommendation.Bucket switch
            {
                RecommendationBucket.Optional => Localizer.Get("optimize.lockedOptional"),
                RecommendationBucket.Experimental => Localizer.Get("optimize.lockedExperimental"),
                RecommendationBucket.SecuritySensitive => Localizer.Get("optimize.lockedSecurity"),
                _ when recommendation.AntiCheatConflictRisk => Localizer.Get("optimize.lockedAntiCheat"),
                _ when recommendation.Compatibility == CompatibilityStatus.Incompatible => Localizer.Get("optimize.lockedIncompatible"),
                _ => string.Empty
            };
        return (isLocked, lockReason);
    }

    private bool CanOperate(string optimizationId) {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var recommendation = uiState.Recommendations.FirstOrDefault(r =>
            r.OptimizationId.Equals(optimizationId, StringComparison.OrdinalIgnoreCase));
        if (recommendation is null) return false;
        if (uiState.AppliedThisSession.Contains(optimizationId)) return false;
        var (isLocked, _) = EvaluateLock(recommendation, uiState.ExpertMode, uiState.Recommendations);
        return !isLocked && recommendation.CurrentState != OptimizationState.AppliedByCao;
    }

    private async Task RunOperationAsync(PrivilegedOperationKind operation, string optimizationId)
    {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        // Un cambio ya aplicado no se puede volver a aplicar: queda como activado.
        if (operation == PrivilegedOperationKind.ApplyOptimization)
        {
            var current = uiState.Recommendations.FirstOrDefault(r =>
                r.OptimizationId.Equals(optimizationId, StringComparison.OrdinalIgnoreCase));
            if (current?.CurrentState == OptimizationState.AppliedByCao
                || uiState.AppliedThisSession.Contains(optimizationId))
            {
                StatusText.Text = "Ya está aplicado — no se puede volver a aplicar. Use Revertir si desea restaurarlo.";
                return;
            }
        }
        var recommendation = uiState.Recommendations.FirstOrDefault(r =>
            r.OptimizationId.Equals(optimizationId, StringComparison.OrdinalIgnoreCase));
        var applying = operation == PrivilegedOperationKind.ApplyOptimization;
        if (uiState.ExpertMode || applying)
        {
            var displayName = recommendation?.NameEs ?? optimizationId;
            var description = recommendation?.DescriptionEs ?? string.Empty;
            var question = applying ? "¿Aplicar este cambio?" : "¿Revertir este cambio?";
            var action = applying ? "aplicarlo" : "revertirlo";
            var content = new StackPanel { Spacing = 10, MaxWidth = 400 };
            content.Children.Add(new CaoCat { Width = 96, HorizontalAlignment = HorizontalAlignment.Center, ShowCaption = false });
            content.Children.Add(new TextBlock { Text = question, FontSize = 20, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center });
            content.Children.Add(new TextBlock { Text = displayName, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            if (!string.IsNullOrWhiteSpace(description))
                content.Children.Add(new TextBlock { Text = description, Opacity = 0.8, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            content.Children.Add(new InfoBar { Severity = InfoBarSeverity.Informational, IsOpen = true, IsClosable = false, Message = $"Antes de {action} guardamos un punto de restauración automático. Si no te gusta, puedes deshacerlo cuando quieras desde la pestaña Restaurar." });
            var dialog = new ContentDialog
            {
                Content = content,
                PrimaryButtonText = applying ? "Sí, aplicar" : "Sí, revertir",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }
        var applySucceeded = false;
        if (applying)
        {
            // Marca inmediata: imposible aplicar dos veces (doble clic) y la UI
            // muestra Activo al instante; se retira si la operación falla.
            uiState.AppliedThisSession.Add(optimizationId);
            Render();
        }

        BusyRing.IsActive = true;
        TransactionProgressCard.Visibility = Visibility.Visible;
        TxRing.IsActive = true;
        TxProgressBar.IsIndeterminate = true;
        TxProgressBar.Value = 0;
        TxPercentText.Text = string.Empty;
        Mascot.Set("Working");
        TxText.Text = operation == PrivilegedOperationKind.ApplyOptimization ? "Aplicando cambio transaccional…" : "Revirtiendo…";
        StatusText.Text = TxText.Text;
        try
        {
            using var cts = new CancellationTokenSource(TimeoutFor(optimizationId));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.SendAsync(operation, optimizationId, cts.Token);
            uiState.ServiceStatus = response is { Accepted: true } ? "connected" : "rejected";
            if (response is { Accepted: true })
            {
                var applied = operation == PrivilegedOperationKind.ApplyOptimization;
                StatusText.Text = applied ? $"✓ {optimizationId} aplicado y verificado. Snapshot disponible para reversión." : $"✓ {optimizationId} revertido y verificado.";
                TxText.Text = "Verificado ✓ — Commit OK";
                TxProgressBar.IsIndeterminate = false;
                TxProgressBar.Value = 100;
                TxPercentText.Text = "100%";
                Mascot.CelebrateThenIdle(DispatcherQueue);
                if (applied) { uiState.AppliedThisSession.Add(optimizationId); applySucceeded = true; }
                else uiState.AppliedThisSession.Remove(optimizationId);
            }
            else
            {
                StatusText.Text = $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta del servicio"}";
                TxText.Text = "Rechazado — transacción no comprometida";
                if (applying) uiState.AppliedThisSession.Remove(optimizationId);
                Mascot.Set("Warn");
                Render();
            }

            // Feedback explícito: diálogo de éxito al aplicar, de resultado al revertir
            if (operation == PrivilegedOperationKind.ApplyOptimization && response is { Accepted: true })
            {
                var appliedRec = uiState.Recommendations.FirstOrDefault(r =>
                    r.OptimizationId.Equals(optimizationId, StringComparison.OrdinalIgnoreCase));
                var needsReboot = appliedRec?.RequiresReboot == true;
                var appliedName = string.IsNullOrWhiteSpace(appliedRec?.NameEs) ? optimizationId : appliedRec!.NameEs;
                var appliedDialog = new ContentDialog
                {
                    Title = "¡Cambio aplicado!",
                    Content = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new CaoCat { Width = 96, Height = 96, Mood = "Celebrate", HorizontalAlignment = HorizontalAlignment.Center },
                            new TextBlock { Text = appliedName, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                            new TextBlock
                            {
                                Text = "Se aplicó y verificó en el sistema." +
                                       (needsReboot ? " Requiere reinicio para efecto completo." : "") +
                                       "\nSnapshot previo guardado: puedes deshacerlo desde Restaurar.",
                                TextWrapping = TextWrapping.Wrap
                            },
                            new InfoBar { Severity = InfoBarSeverity.Success, IsOpen = true, IsClosable = false, Message = "Ya figura como Activo — no se puede volver a aplicar." },
                        }
                    },
                    CloseButtonText = "Aceptar",
                    CloseButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };
                await appliedDialog.ShowAsync();
            }
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

            try
            {
                using var refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _vm.RefreshRecommendationsAsync(refreshCts.Token);
            }
            catch (Exception refreshEx)
            {
                App.WriteCrashLog(refreshEx);
            }
            // La UI se repinta SIEMPRE: si el refresh falla, la marca local
            // mantiene el Activo y la tarjeta no reaparece como pendiente.
            if (applySucceeded)
                uiState.AppliedThisSession.Add(optimizationId);
            Render();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Operación cancelada.";
            TxText.Text = "Cancelado";
            if (applying) uiState.AppliedThisSession.Remove(optimizationId);
            Mascot.Set("Warn");
            Render();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Servicio no disponible: {ex.Message} (CAO-IPC-004 — verifique que CA-O Service esté instalado)";
            TxText.Text = "Servicio no disponible";
            if (applying) uiState.AppliedThisSession.Remove(optimizationId);
            Mascot.Set("Warn");
            Render();
            App.WriteCrashLog(ex);
        }
        finally
        {
            BusyRing.IsActive = false;
            TxRing.IsActive = false;
            await Task.Delay(1200);
            TransactionProgressCard.Visibility = Visibility.Collapsed;
        }
    }

    private static TimeSpan TimeoutFor(string optimizationId) => optimizationId switch
    {
        "windows-component-store-cleanup" or "windows-component-store-resetbase"
            or "optimize-system-drive" or "retrim-system-ssd" or "defragment-hdd-only"
            or "disk-cleanup-system-files" or "cleanup-windows-update-cache" or "reset-network-stack-repair" or "repair-windows-update" => TimeSpan.FromMinutes(20),
        _ => TimeSpan.FromSeconds(60),
    };

    private async void OnApplyRecommendedClick(object sender, RoutedEventArgs e)
    {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var recommended = uiState.Recommendations
            .Where(recommendation => recommendation.Bucket == RecommendationBucket.Recommended &&
                                     recommendation.CurrentState != OptimizationState.AppliedByCao &&
                                     !uiState.AppliedThisSession.Contains(recommendation.OptimizationId))
            .Select(recommendation => recommendation.OptimizationId)
            .ToList();

        if (recommended.Count == 0)
        {
            StatusText.Text = "No hay recomendaciones pendientes; ejecute el análisis primero.";
            return;
        }

        BusyRing.IsActive = true;
        TransactionProgressCard.Visibility = Visibility.Visible;
        TxRing.IsActive = true;
        TxProgressBar.IsIndeterminate = false;
        TxProgressBar.Value = 0;
        TxPercentText.Text = "0%";
        Mascot.Set("Working");
        var failures = new List<string>();
        var appliedOk = new List<string>();
        try
        {
            for (var i = 0; i < recommended.Count; i++)
            {
                var id = recommended[i];
                TxText.Text = $"Aplicando {id} ({i + 1}/{recommended.Count})…";
                StatusText.Text = TxText.Text;
                TxProgressBar.Value = (double)i / recommended.Count * 100;
                TxPercentText.Text = $"{TxProgressBar.Value:0}%";
                using var cts = new CancellationTokenSource(TimeoutFor(id));
                var pipe = AppHost.Resolve<PrivilegedPipeClient>();
                var response = await pipe.SendAsync(PrivilegedOperationKind.ApplyOptimization, id, cts.Token);
                if (response is not { Accepted: true })
                {
                    failures.Add($"{id}: [{response?.ErrorCode}] {response?.SafeMessage ?? "sin respuesta"}");
                    break; // stop the batch on first failure (spec 124)
                }
                appliedOk.Add(id);
                uiState.AppliedThisSession.Add(id);
                TxProgressBar.Value = (double)appliedOk.Count / recommended.Count * 100;
                TxPercentText.Text = $"{TxProgressBar.Value:0}%";
            }
            foreach (var ok in appliedOk) uiState.AppliedThisSession.Add(ok);

            try
            {
                using var refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _vm.RefreshRecommendationsAsync(refreshCts.Token);
            }
            catch (Exception refreshEx)
            {
                App.WriteCrashLog(refreshEx);
            }
            foreach (var ok in appliedOk) uiState.AppliedThisSession.Add(ok);
            Render();
            StatusText.Text = failures.Count == 0
                ? $"✓ Aplicados {appliedOk.Count} cambios recomendados y verificados. Figuran como Activos."
                : $"Lote detenido: {string.Join("; ", failures)}";
            TxText.Text = failures.Count == 0 ? $"Verificado ✓ — {appliedOk.Count}/{recommended.Count} aplicados" : "Lote detenido — revisa el fallo";
            TxProgressBar.Value = failures.Count == 0 ? 100 : (double)appliedOk.Count / recommended.Count * 100;
            TxPercentText.Text = $"{TxProgressBar.Value:0}%";
            if (failures.Count == 0) Mascot.CelebrateThenIdle(DispatcherQueue);
            else Mascot.Set("Warn");
            var batchDialog = new ContentDialog
            {
                Title = failures.Count == 0 ? $"✓ {appliedOk.Count} cambios aplicados" : "Lote detenido",
                Content = new TextBlock
                {
                    Text = failures.Count == 0
                        ? $"Se aplicaron y verificaron {appliedOk.Count} cambios.\nYa figuran como Activos y no se pueden volver a aplicar.\nSnapshots disponibles en Restaurar."
                        : $"Se aplicaron {appliedOk.Count} antes del fallo y el lote se DETUVO (los ya aplicados SIGUEN aplicados; revierte cada uno en Restaurar si lo necesitas).\nFallo: {string.Join("; ", failures)}",
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = "Aceptar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            await batchDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Servicio no disponible: {ex.Message}";
            TxText.Text = "Servicio no disponible";
            Mascot.Set("Warn");
            Render();
            App.WriteCrashLog(ex);
        }
        finally
        {
            BusyRing.IsActive = false;
            TxRing.IsActive = false;
            await Task.Delay(1500);
            TransactionProgressCard.Visibility = Visibility.Collapsed;
        }
    }
}
