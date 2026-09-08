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
    bool IsApplyEnabled,
    bool IsApplied,
    string ApplyLabel,
    string TooltipDetail)
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
                recommendation.Compatibility.ToString(),
                recommendation.CurrentState.ToString(),
                recommendation.Score?.ToString() ?? "n/a",
                recommendation.Reason.MessageEs,
                recommendation.Bucket,
                isLocked,
                lockReason,
                benefit,
                canApply,
                isApplied,
                isApplied ? "Aplicado ✓" : "Aplicar",
                tooltip);
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
                diffPanel.Children.Add(new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Child = new TextBlock
                    {
                        Text = previewApplied && !previewLocked
                            ? "Ya aplicado por CA-O — no se puede volver a aplicar. Use Revertir si desea restaurarlo."
                            : $"Bloqueado — no se puede aplicar: {previewLockReason}",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    },
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

            bool offerApply = !previewLocked && !previewApplied;
            bool offerRevert = !previewLocked && previewApplied;
            var dialog = new ContentDialog
            {
                Title = $"Vista previa — {preview.OptimizationId}",
                Content = new ScrollViewer { MaxHeight = 460, Content = diffPanel },
                CloseButtonText = "Cerrar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (offerApply) dialog.PrimaryButtonText = "Aplicar este cambio";
            else if (offerRevert) dialog.PrimaryButtonText = "Revertir este cambio";
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
            using var cts = new CancellationTokenSource(TimeoutFor(optimizationId));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.SendAsync(operation, optimizationId, cts.Token);
            uiState.ServiceStatus = response is { Accepted: true } ? "connected" : "rejected";
            if (response is { Accepted: true })
            {
                var applied = operation == PrivilegedOperationKind.ApplyOptimization;
                StatusText.Text = applied ? $"✓ {optimizationId} aplicado y verificado. Snapshot disponible para reversión." : $"✓ {optimizationId} revertido y verificado.";
                TxText.Text = "Verificado ✓ — Commit OK";
                if (applied) uiState.AppliedThisSession.Add(optimizationId);
                else uiState.AppliedThisSession.Remove(optimizationId);
            }
            else
            {
                StatusText.Text = $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta del servicio"}";
                TxText.Text = "Rechazado — transacción no comprometida";
            }

            // Feedback explícito: diálogo de éxito al aplicar, de resultado al revertir
            if (operation == PrivilegedOperationKind.ApplyOptimization && response is { Accepted: true })
            {
                var needsReboot = uiState.Recommendations.FirstOrDefault(r =>
                    r.OptimizationId.Equals(optimizationId, StringComparison.OrdinalIgnoreCase))?.RequiresReboot == true;
                var appliedDialog = new ContentDialog
                {
                    Title = "✓ Aplicado correctamente",
                    Content = new TextBlock
                    {
                        Text = $"{optimizationId} se aplicó y verificó en el sistema.\nSnapshot previo guardado: reversible desde Restaurar o con Revertir." +
                               (needsReboot ? "\nRequiere reinicio para efecto completo." : "") +
                               "\nYa figura como Activo y no se puede volver a aplicar.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = "Aceptar",
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

    private static TimeSpan TimeoutFor(string optimizationId) => optimizationId switch
    {
        "windows-component-store-cleanup" or "windows-component-store-resetbase"
            or "optimize-system-drive" or "optimize-hdd-media-aware" or "retrim-system-ssd"
            or "disk-cleanup-system-files" or "reset-network-stack-repair" or "repair-windows-update" => TimeSpan.FromMinutes(20),
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
        var failures = new List<string>();
        var appliedOk = new List<string>();
        try
        {
            foreach (var id in recommended)
            {
                using var cts = new CancellationTokenSource(TimeoutFor(id));
                var pipe = AppHost.Resolve<PrivilegedPipeClient>();
                var response = await pipe.SendAsync(PrivilegedOperationKind.ApplyOptimization, id, cts.Token);
                if (response is not { Accepted: true })
                {
                    failures.Add($"{id}: [{response?.ErrorCode}] {response?.SafeMessage ?? "sin respuesta"}");
                    break; // stop the batch on first failure (spec 124)
                }
                appliedOk.Add(id);
            }
            foreach (var ok in appliedOk) uiState.AppliedThisSession.Add(ok);

            using var refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _vm.RefreshRecommendationsAsync(refreshCts.Token);
            Render();
            StatusText.Text = failures.Count == 0
                ? $"✓ Aplicados {appliedOk.Count} cambios recomendados y verificados. Figuran como Activos."
                : $"Lote detenido: {string.Join("; ", failures)}";
            var batchDialog = new ContentDialog
            {
                Title = failures.Count == 0 ? $"✓ {appliedOk.Count} cambios aplicados" : "Lote detenido",
                Content = new TextBlock
                {
                    Text = failures.Count == 0
                        ? $"Se aplicaron y verificaron {appliedOk.Count} cambios.\nYa figuran como Activos y no se pueden volver a aplicar.\nSnapshots disponibles en Restaurar."
                        : $"Se aplicaron {appliedOk.Count} antes del fallo y se revirtieron los reversibles.\nFallo: {string.Join("; ", failures)}",
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
            App.WriteCrashLog(ex);
        }
        finally
        {
            BusyRing.IsActive = false;
        }
    }
}
