#pragma warning disable CA2016
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CAO.Core.Diagnostics;
using CAO.Infrastructure.Networking;
using CAO.Infrastructure.Security;
using CAO.Infrastructure.Storage;
using CAO.Infrastructure.SystemInterop;
using CAO.Shared;
using CAO.UI.ViewModels;

namespace CAO.UI.Pages;

/// <summary>Diagnostics-first page ( §6-10, §8 WhenAll ): mide en paralelo con estado por módulo y cancelación.</summary>
public sealed partial class AnalyzePage : Page
{
    private sealed record FindingRow(string SeverityLabel, string MessageEs, Brush SeverityBrush);

    private readonly AnalyzeViewModel _viewModel;
    private readonly ViewModels.DiagnosticsViewModel _diagnosticsVm;
    private readonly ViewModels.GamingViewModel _gamingVm;
    private CancellationTokenSource? _cts;
    private DnsBenchmarkResult? _bestDns;
    private DnsBenchmarkResult? _secondDns;
    private double? _lastDpcMax;
    private List<DnsRowSnapshot>? _lastDnsRows;
    private List<StorageRowSnapshot>? _lastStorageRows;

    public AnalyzePage()
    {
        InitializeComponent();
        _viewModel = AppHost.Resolve<AnalyzeViewModel>();
        _diagnosticsVm = AppHost.Resolve<ViewModels.DiagnosticsViewModel>();
        _gamingVm = AppHost.Resolve<ViewModels.GamingViewModel>();
        DataContext = _viewModel;
        Loaded += (_, _) => LoadPersisted();
        _diagnosticsVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.DiagnosticsViewModel.InputSummary) or nameof(ViewModels.DiagnosticsViewModel.ThermalSummary) or nameof(ViewModels.DiagnosticsViewModel.PerformanceSummary))
                DispatcherQueue.TryEnqueue(RenderDiagnostics);
        };
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModels.UiState.Language))
                DispatcherQueue.TryEnqueue(ApplyTexts);
        };
        ApplyTexts();
    }

    private void ApplyTexts()
    {
        // Localized static texts via Localizer
        try { NoteBar.Title = Localizer.Get("analyze.diagnosticsFirst"); } catch { }
        try { NoteBar.Message = Localizer.Get("analyze.diagnosticsFirstMessage"); } catch { }
        try { RunButton.Content = Localizer.Get("analyze.runFull"); AutomationProperties.SetName(RunButton, Localizer.Get("analyze.runFull")); } catch { }
        try { CancelButton.Content = Localizer.Get("common.cancel"); AutomationProperties.SetName(CancelButton, Localizer.Get("common.cancel")); } catch { }
        try { DnsActionButton.Content = Localizer.Get("analyze.applyDns"); } catch { }
        try { Helpers.LocalizationHelper.LocalizeTree(this.Content as Microsoft.UI.Xaml.DependencyObject ?? this); } catch { }
        if (_viewModel != null)
        {
            var state = AppHost.Resolve<ViewModels.UiState>();
            var store = AppHost.Resolve<CAO.Infrastructure.Persistence.AnalysisStateStore>();
            var session = AppHost.Resolve<CAO.Infrastructure.Persistence.AnalysisSessionService>();
            var fp = state.Context != null ? CAO.Infrastructure.Persistence.AnalysisStateStore.ComputeGamesFingerprint(state.Context.GamesDetected) : null;
            var (fresh, reason, age) = store.GetFreshness(session.GetLastAnalysis(), state.Context, fp);
            state.FreshnessLabel = store.GetFreshnessLabel(fresh, reason, age);
        }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadPersisted();
        var uiStateNav = AppHost.Resolve<ViewModels.UiState>();
        if (uiStateNav.DisplaySnapshot is not null)
        {
            // Foto vigente: restauración instantánea sin re-medir nada.
            RestoreDisplaySnapshot();
        }
        else
        {
            // Hydrate VM without ResetResults if persisted analysis exists
            var session = AppHost.Resolve<CAO.Infrastructure.Persistence.AnalysisSessionService>();
            var persisted = session.GetLastAnalysis();
            if (persisted?.Context != null)
            {
                _viewModel.HydrateFromPersistedAnalysis(persisted);
                RenderFromViewModel();
            }
        }
        ApplyTexts();
        UpdateFreshnessBanner();
        CAO.UI.Helpers.UiAnimations.PlayEntrance(PageContent);
    }

    private void UpdateFreshnessBanner()
    {
        var state = AppHost.Resolve<ViewModels.UiState>();
        var store = AppHost.Resolve<CAO.Infrastructure.Persistence.AnalysisStateStore>();
        var session = AppHost.Resolve<CAO.Infrastructure.Persistence.AnalysisSessionService>();
        var persisted = session.GetLastAnalysis();
        var fp = state.Context != null ? CAO.Infrastructure.Persistence.AnalysisStateStore.ComputeGamesFingerprint(state.Context.GamesDetected) : null;
        var (fresh, reason, age) = store.GetFreshness(persisted, state.Context, fp);
        if (fresh == CAO.Infrastructure.Persistence.AnalysisFreshness.Unavailable)
        {
            NoteBar.Severity = InfoBarSeverity.Informational;
            NoteBar.Title = Localizer.Get("analyze.noAnalysis");
            NoteBar.Message = Localizer.Get("analyze.noAnalysisMessage");
            NoteBar.IsOpen = true;
        }
        else if (fresh == CAO.Infrastructure.Persistence.AnalysisFreshness.Fresh)
        {
            NoteBar.Severity = InfoBarSeverity.Success;
            NoteBar.Title = Localizer.Get("analyze.fresh");
            NoteBar.Message = $"{Localizer.Format("analyze.lastAnalysis", (int)age.TotalDays)} · {Localizer.Get("analyze.freshMessage")}";
            NoteBar.IsOpen = true;
        }
        else if (fresh == CAO.Infrastructure.Persistence.AnalysisFreshness.Stale && reason == CAO.Infrastructure.Persistence.StaleReason.GameInventoryChanged)
        {
            NoteBar.Severity = InfoBarSeverity.Warning;
            NoteBar.Title = Localizer.Get("analyze.gameChanged");
            NoteBar.Message = Localizer.Get("analyze.gameChangedMessage");
            NoteBar.IsOpen = true;
        }
        else if (fresh == CAO.Infrastructure.Persistence.AnalysisFreshness.Stale)
        {
            NoteBar.Severity = InfoBarSeverity.Warning;
            NoteBar.Title = Localizer.Get("analyze.stale");
            NoteBar.Message = Localizer.Get("analyze.staleMessage");
            NoteBar.IsOpen = true;
        }
        else if (fresh == CAO.Infrastructure.Persistence.AnalysisFreshness.VeryStale)
        {
            NoteBar.Severity = InfoBarSeverity.Warning;
            NoteBar.Title = Localizer.Get("analyze.veryStale");
            NoteBar.Message = Localizer.Get("analyze.veryStaleMessage");
            NoteBar.IsOpen = true;
        }
    }

    private void LoadPersisted()
    {
        var state = AppHost.Resolve<ViewModels.UiState>();
        if (state.Context is not { } ctx)
        {
            CollapseDataCards();
            UpdateResultsVisibility();
            return;
        }
        // Datos persistentes: mostrar sin necesidad de re-ejecutar análisis
        // Gaming bloqueos persistentes
        var recs = state.Recommendations;
        var blocked = recs.Where(r => r.AntiCheatConflictRisk || r.Bucket == RecommendationBucket.SecuritySensitive).ToList();
        if (blocked.Count > 0)
        {
            GamingBlockedText.Text = string.Join("\n", blocked.Select(b => $"• {b.OptimizationId} — {b.NameEs}  [candado] {b.Reason.MessageEs}"));
        }
        else if (ctx.AntiCheats.Count > 0)
        {
            GamingBlockedText.Text = "Anti-cheat detectado pero ninguna optimización bloqueada en este perfil.";
        }
        else
        {
            GamingBlockedText.Text = "Sin anti-cheat detectado — todas las optimizaciones disponibles según perfil.";
        }
        GamingGamesText.Text = ctx.GamesDetected.Count == 0 ? "Juegos: ninguno detectado" : $"Juegos: {string.Join(", ", ctx.GamesDetected)}";
        if (ctx.AntiCheats.Count > 0)
            GamingGamesText.Text += $"\nAnti-cheats: {string.Join(", ", ctx.AntiCheats.Select(a => a.Kind.ToString()))}";

        StatusText.Text = state.LastAnalysisUtc is null ? "Datos del último análisis cargados." : $"Datos del {state.LastAnalysisUtc.Value.ToLocalTime():g} cargados.";
        UpdateFreshnessBanner();
        RenderDiagnostics();
        RenderHealth();
        CollapseDataCards();
        UpdateResultsVisibility();
    }

    /// <summary>Salud del sistema + hardware + hallazgos (vive aquí, se refresca con cada análisis).</summary>
    private void RenderHealth()
    {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var context = uiState.Context;
        var health = _viewModel.Health ??
            (context is null ? null : HealthEngine.Evaluate(context));

        if (context is null)
        {
            SystemHealthText.Text = "Sistema: sin datos";
            SystemHealthBadge.Background = (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];
            HealthScoresText.Text = "";
            WhyScoresButton.Visibility = Visibility.Collapsed;
            EmptyFindingsState.Visibility = Visibility.Visible;
            FindingsList.Visibility = Visibility.Collapsed;
            return;
        }

        // Scores + hallazgos
        if (health is null)
        {
            HealthScoresText.Text = "";
            WhyScoresButton.Visibility = Visibility.Collapsed;
            EmptyFindingsState.Visibility = Visibility.Visible;
            FindingsList.Visibility = Visibility.Collapsed;
            return;
        }

        var measured = health.Scores.Where(score => score.IsMeasured && score.Score is not null).ToList();
        HealthScoresText.Text = measured.Count == 0
            ? "Sin puntuación — faltan mediciones."
            : string.Join("  ·  ", measured.Select(score => $"{DimensionEs(score.Dimension)}: {score.Score}/100"));
        WhyScoresButton.Visibility = string.IsNullOrWhiteSpace(HealthScoresText.Text) ? Visibility.Collapsed : Visibility.Visible;
        var findings = health.Findings.Select(f => new FindingRow(f.Severity.ToString(), f.MessageEs, BrushFor(f.Severity.ToString()))).ToList();
        var critCount = findings.Count(f => f.SeverityLabel.Equals("Critical", StringComparison.OrdinalIgnoreCase));
        var warnCount = findings.Count(f => f.SeverityLabel.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        FindingsCountText.Text = findings.Count == 0
            ? "Sin hallazgos = sin problemas detectados."
            : $"{critCount} críticas · {warnCount} avisos · {findings.Count} en total.";
        FindingsList.ItemsSource = findings;
        FindingsList.Visibility = findings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyFindingsState.Visibility = findings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SystemHealthText.Text = DeriveSystemStatus(health);
        SystemHealthBadge.Background = BrushForStatus(SystemHealthText.Text);
        UpdateAnalysisFooter();
    }

    private void UpdateAnalysisFooter()
    {
        try
        {
            var uiState = AppHost.Resolve<ViewModels.UiState>();
            if (uiState.LastAnalysisUtc is null)
            {
                AnalysisFooterText.Text = "Sin análisis registrado aún.";
                return;
            }
            var when = uiState.LastAnalysisUtc.Value.ToLocalTime().ToString("g");
            var rec = uiState.Recommendations.Count(r => r.Bucket == RecommendationBucket.Recommended);
            AnalysisFooterText.Text = $"Último análisis: {when} · {rec} recomendadas · {uiState.Recommendations.Count} evaluadas";
        }
        catch { }
    }

    private static string DimensionEs(object dimension) => dimension.ToString() switch
    {
        "System" => "Sistema",
        "Thermals" => "Térmica",
        "Network" => "Red",
        "Storage" => "Disco",
        "Security" => "Seguridad",
        "Input" => "Entrada",
        "Gaming" => "Gaming",
        "Startup" => "Inicio",
        "Stability" => "Estabilidad",
        _ => dimension.ToString() ?? "?",
    };

    private static Brush BrushFor(string severity) => severity.ToLowerInvariant() switch
    {
        "error" or "critical" => (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
        "warning" => (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
        _ => (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
    };

    private static Brush BrushForStatus(string status) => status.Contains("Atención") || status.Contains("Attention")
        ? (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
        : status.Contains("Correcto") || status.Contains("Healthy")
        ? (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
        : (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];

    private static string DeriveSystemStatus(SystemDiagnosticReport report)
    {
        if (report.Findings.Any(f => f.Severity.ToString().Equals("Critical", StringComparison.OrdinalIgnoreCase))) return "Sistema: Atención";
        if (report.Findings.Any(f => f.Severity.ToString().Equals("Warning", StringComparison.OrdinalIgnoreCase))) return "Sistema: Correcto con avisos";
        return "Sistema: Correcto";
    }

    private async void OnWhyScoresClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Health is null) return;
        var detail = string.Join("\n", _viewModel.Health.Scores.Where(s => s.IsMeasured).Select(s => $"• {s.Dimension}: {s.Score}/100 — {s.ReasonEs}"));
        if (string.IsNullOrWhiteSpace(detail)) detail = "No hay dimensiones medidas suficientes para un score. Ejecute más diagnósticos.";
        var dialog = new ContentDialog
        {
            Title = "Desglose de salud del sistema",
            Content = new ScrollViewer { MaxHeight = 380, Content = new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } },
            CloseButtonText = "Cerrar",
            XamlRoot = Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private void RenderDiagnostics()
    {
        // Sin datos no hay tarjeta: nada de placeholders en blanco.
        DiagnInputText.Text = _diagnosticsVm.InputSummary ?? "";
        DiagnThermalText.Text = _diagnosticsVm.ThermalSummary ?? "";
        DiagnPerfText.Text = _diagnosticsVm.PerformanceSummary ?? "";
    }

    /// <summary>Las secciones de resultados solo existen con análisis vigente o en curso.</summary>
    private bool _forceResults;
    private bool HasAnalysis() =>
        AppHost.Resolve<ViewModels.UiState>().LastAnalysisUtc is not null;

    private void UpdateResultsVisibility(bool running = false)
    {
        var show = running || _forceResults || HasAnalysis();
        ResultsContent.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        HowItWorksCard.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void CollapseIfEmpty(Border card, TextBlock text)
    {
        card.Visibility = string.IsNullOrWhiteSpace(text.Text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CollapseDataCards()
    {
        CollapseIfEmpty(CardNetwork, NetworkText);
        CollapseIfEmpty(CardSecurity, SecurityText);
        CollapseIfEmpty(CardStorage, StorageText);
        CollapseIfEmpty(CardDrivers, DriversText);
        CollapseIfEmpty(CardInput, DiagnInputText);
        CollapseIfEmpty(CardThermal, DiagnThermalText);
        CollapseIfEmpty(CardPerf, DiagnPerfText);
        CollapseIfEmpty(CardGaming, GamingBlockedText);
    }

    private async void OnRunClick(object sender, RoutedEventArgs e)
    {
        if (!RunButton.IsEnabled) return;
        await RunFullAnalysisAsync();
    }

    /// <summary>Ejecuta el análisis completo (llamable desde el Panel).</summary>
    public async Task RunFullAnalysisAsync()
    {
        if (!RunButton.IsEnabled) return;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        RunButton.IsEnabled = false;
        CancelButton.Visibility = Visibility.Visible;
        Ring.IsActive = true;
        ProgressCard.Visibility = Visibility.Visible;
        StatusText.Text = "Midiendo…";
        UpdateResultsVisibility(running: true);
        SetProgress(true);
        try
        {
            // Ejecución concurrente con WhenAll y CancellationToken ( §8 ) vía ViewModel
            var results = await _viewModel.RunAsync(_cts.Token);

            // Render parcial tolerante a fallos individuales ( §10 )
            RenderFromViewModel();
            LoadPersisted();
            // Auto-ejecutar diagnósticos integrados + DNS benchmark y DPC como parte del análisis completo
            try { await _diagnosticsVm.RunCommand.ExecuteAsync(null); RenderDiagnostics(); } catch { }
            try { await RunDnsBenchmarkAuto(_cts.Token); } catch { }
            try { await RunDpcAuto(_cts.Token); } catch { }
            // Escaneo gaming integrado (la pestaña Gaming vive aquí ahora)
            try { await _gamingVm.ScanCommand.ExecuteAsync(null); RenderGamingScan(); } catch { }
            // Foto de todo lo renderizado (textos, DNS, DPC, gaming) a sesión + disco.
            PersistDisplaySnapshot();

            var failed = results.Count(r => r.Status == ViewModels.AnalysisModuleStatus.Failed);
            var cancelled = results.Count(r => r.Status == ViewModels.AnalysisModuleStatus.Cancelled);
            StatusText.Text = cancelled > 0 ? _viewModel.OverallStatus
                : failed == 0 ? "Análisis completo — ningún cambio aplicado."
                : $"Análisis completado con advertencias ({failed} módulos con fallo)";

            // Compatibilidad: mantener lectura directa si ViewModel no pobló algo (lazy)
            if (string.IsNullOrWhiteSpace(NetworkText.Text))
            {
                var network = await WithRing(NetRing, () => new NetworkDiagnosticsProvider().MeasureAsync(_cts.Token));
                NetworkText.Text = "Interfaces: " + string.Join(", ", network.Interfaces) + "\n" + string.Join("\n",
                    network.Measurements.Select(measurement =>
                        $"{measurement.Kind} {measurement.Endpoint}: " +
                        (measurement.MedianLatencyMs is null ? "sin respuesta" :
                            $"{measurement.MedianLatencyMs:0.0} ms, jitter {measurement.JitterMs:0.0} ms, pérdida {measurement.Attempts - measurement.SuccessfulAttempts}/{measurement.Attempts}")));
                NetStatusBadge.Visibility = Visibility.Visible;
                NetStatusText.Text = network.Measurements.Any(m => m.MedianLatencyMs is not null) ? "Medido" : "Sin datos";
                SetSubtleBadge(NetStatusBadge, NetStatusText,
                    network.Measurements.Any(m => m.MedianLatencyMs is not null) ? "CaoGreenSoftBrush" : "CaoSlateSoftBrush",
                    network.Measurements.Any(m => m.MedianLatencyMs is not null) ? "CaoGreenSolidBrush" : "CaoNeutralBrush");
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Análisis cancelado.";
        }
        catch (Exception ex)
        {
            var err = ErrorTranslator.Translate(ex, CAO.Shared.Correlation.New());
            NetworkText.Text = $"{err.Code}: {err.UserMessageEs} {err.RecoveryActionEs} [Técnico: {err.TechnicalMessage}]";
            StatusText.Text = $"{err.Code}: medición de red fallida";
            CAO.UI.App.WriteCrashLog(ex);
            try { AppHost.Resolve<Infrastructure.Logging.StructuredLogger>().Error("Analyze", err.UserMessageEs, ex, err.CorrelationId, err.Code); } catch { }
        }
        finally
        {
            Ring.IsActive = false;
            RunButton.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed;
            SetProgress(false);
            CollapseDataCards();
            UpdateResultsVisibility();
            _viewModel.CancelCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
        _viewModel.CancelCommand.Execute(null);
    }

    private async void RenderFromViewModel()
    {
        // Network
        if (_viewModel.NetworkResult is { } net)
        {
            if (net.Status == ViewModels.AnalysisModuleStatus.Completed)
            {
                NetworkText.Text = $"Network: {net.Value} ({net.Duration.TotalMilliseconds:0} ms)";
                NetStatusBadge.Visibility = Visibility.Visible;
                NetStatusText.Text = "Medido";
                SetSubtleBadge(NetStatusBadge, NetStatusText, "CaoGreenSoftBrush", "CaoGreenSolidBrush");
            }
            else if (net.Status == ViewModels.AnalysisModuleStatus.Failed)
            {
                NetworkText.Text = $"{ErrorCodes.UiAnalyzeFailed}: {net.Message}";
                NetStatusText.Text = "Fallo";
                NetStatusBadge.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            }
            else if (net.Status == ViewModels.AnalysisModuleStatus.Cancelled)
            {
                NetworkText.Text = "Medición de red cancelada.";
            }
        }
        // Security
        if (_viewModel.SecurityResult is { } sec)
        {
            if (sec.Status == ViewModels.AnalysisModuleStatus.Completed)
            {
                var security = new SecurityDiagnosticsProvider().Measure();
                SecurityText.Text = string.Join("\n", security.Features.Select(feature =>
                        $"• {feature.Name}: {(feature.Enabled is null ? "desconocido" : feature.Enabled.Value ? "activado" : "desactivado")}")) +
                    $"\nVanguard: {(security.VanguardDetected ? "detectado" : "no detectado")}";
                SecStatusBadge.Visibility = Visibility.Visible;
                SecStatusText.Text = security.VanguardDetected ? "Anti-cheat detectado" : "Sin anti-cheat";
                SetSubtleBadge(SecStatusBadge, SecStatusText,
                    security.VanguardDetected ? "CaoAmberSoftBrush" : "CaoGreenSoftBrush",
                    security.VanguardDetected ? "CaoAmberSolidBrush" : "CaoGreenSolidBrush");
            }
            else if (sec.Status == ViewModels.AnalysisModuleStatus.Failed)
            {
                SecurityText.Text = $"{ErrorCodes.UiAnalyzeFailed}: {sec.Message}";
            }
        }
        // Storage
        if (_viewModel.StorageResult is { } stor)
        {
            if (stor.Status == ViewModels.AnalysisModuleStatus.Completed)
            {
                const double gib = 1024d * 1024 * 1024;
                var storage = new StorageDiagnosticsProvider().Measure();
                StorageText.Text = string.Join("\n", storage.Volumes.Select(volume =>
                    $"• {volume.Name} {volume.FileSystem}: libre {volume.FreeBytes / gib:0.0} de {volume.TotalBytes / gib:0.0} GB{(volume.IsSystemVolume ? " [sistema]" : "")}"));
                RenderStorageBars(storage.Volumes);
            }
        }
        // Drivers
        if (_viewModel.DriversResult is { } drv)
        {
            if (drv.Status == ViewModels.AnalysisModuleStatus.Completed)
            {
                try
                {
                    var report = await new DriverDiagnosticsProvider().MeasureAsync(CancellationToken.None);
                    var problem = report.Drivers.Where(d => d.ProblemCode != 0 || d.IsSigned == false || (d.Status != null && !d.Status.Equals("OK", StringComparison.OrdinalIgnoreCase))).Take(8).ToList();
                    if (report.Drivers.Count == 0)
                    {
                        DriversText.Text = "Sin enumeración de drivers en este análisis.";
                    }
                    else if (problem.Count == 0)
                    {
                        DriversText.Text = $"Revisados {report.Drivers.Count} drivers — sin códigos de problema. Todos firmados y estado OK. Si un juego falla, verifica GPU/red/audio con el fabricante.";
                    }
                    else
                    {
                        DriversText.Text = $"Revisados {report.Drivers.Count} drivers — {problem.Count} con incidencia:\n" + string.Join("\n", problem.Select(p => $"• {p.Name} ({p.DeviceClass}) v{p.Version} — {(p.ProblemCode != 0 ? $"código {p.ProblemCode}" : p.IsSigned == false ? "sin firma" : p.Status)}"));
                    }
                }
                catch
                {
                    DriversText.Text = drv.Value ?? "Drivers medidos";
                }
            }
            else if (drv.Status == ViewModels.AnalysisModuleStatus.Failed)
            {
                DriversText.Text = $"{ErrorCodes.UiDiagnosticsFailed}: {drv.Message}\nQué hace: lista drivers con problema (código ConfigManager, sin firma, detenidos) para descartar causa de stutter/crashes.";
            }
        }
    }

    private static void SetSubtleBadge(Border badge, TextBlock label, string bgKey, string fgKey)
    {
        try
        {
            badge.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[bgKey];
            label.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[fgKey];
        }
        catch { }
    }

    private void RenderStorageBars(System.Collections.Generic.IReadOnlyList<StorageVolumeReport> volumes)
    {
        try
        {
            StorageBarsPanel.Children.Clear();
            var rows = volumes.Where(v => v.TotalBytes > 0).Take(4).ToList();
            _lastStorageRows = rows.Select(volume => new StorageRowSnapshot(
                volume.Name,
                (1 - (double)volume.FreeBytes / volume.TotalBytes) * 100,
                volume.FreeBytes / 1024d / 1024 / 1024)).ToList();
            foreach (var row in _lastStorageRows)
            {
                AddBarRow(StorageBarsPanel, row.Name, row.UsedPct, 100, $"{row.FreeGb:0} GB libres");
            }
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
    }

    private static async Task<T> WithRing<T>(ProgressRing ring, Func<Task<T>> work)
    {
        ring.IsActive = true;
        try { return await work(); } finally { ring.IsActive = false; }
    }

    private void SetProgress(bool active)
    {
        CpuRing.IsActive = active; GpuRing.IsActive = active; MemRing.IsActive = active;
        StorRing.IsActive = active; NetRing.IsActive = active; SecRing.IsActive = active; DrvRing.IsActive = active;
        if (!active) { CpuRing.IsActive = false; GpuRing.IsActive = false; MemRing.IsActive = false; }
    }

    private async void OnDnsBenchClick(object sender, RoutedEventArgs e)
    {
        // Si ya hay un mejor DNS detectado, aplicar
        if (_bestDns != null)
        {
            await ApplyBestDnsAsync();
            return;
        }
        await RunDnsBenchmarkAuto(CancellationToken.None);
    }

    private async Task RunDnsBenchmarkAuto(CancellationToken ct)
    {
        try
        {
            DnsBestText.Text = "Benchmark DNS en curso...";
            var results = await new DnsBenchmarkProvider().BenchmarkAsync(null, ct);
            var best = DnsBenchmarkProvider.PickBest(results);
            // Secundario: segundo mejor con respuesta (el primario solo no basta).
            var second = results
                .Where(result => result.MedianLatencyMs is not null && result.Successes >= Math.Max(1, result.Attempts / 2))
                .OrderBy(result => result.MedianLatencyMs)
                .FirstOrDefault(result => best is null || !result.Resolver.Equals(best.Resolver, StringComparison.OrdinalIgnoreCase));
            _bestDns = best;
            _secondDns = second?.Resolver == best?.Resolver ? null : second;
            NetworkText.Text = "DNS benchmark:\n" + string.Join("\n", results.Select(result =>
                $"• {result.Resolver}: " +
                (result.MedianLatencyMs is null ? "sin respuesta" : $"{result.MedianLatencyMs:0.0} ms") +
                $", éxito {result.Successes}/{result.Attempts}")) +
                (best is null ? "\nSin datos suficientes para recomendar." : $"\nMejor medido: {best.Resolver} (recomendación basada en medición).");
            RenderDnsBars(results);
            if (best != null)
            {
                DnsBestText.Text = _secondDns is null
                    ? $"Mejor DNS: {best.Resolver} ({best.MedianLatencyMs:0.0} ms) — pulsa de nuevo para aplicar"
                    : $"Primario: {best.Resolver} ({best.MedianLatencyMs:0.0} ms) · Secundario: {_secondDns.Resolver} ({_secondDns.MedianLatencyMs:0.0} ms) — pulsa de nuevo para aplicar ambos";
                DnsActionButton.Content = $"Aplicar DNS {best.Resolver}";
            }
            else
            {
                DnsBestText.Text = "Sin DNS recomendado";
            }
        }
        catch (OperationCanceledException) { DnsBestText.Text = "Benchmark cancelado"; }
        catch (Exception ex)
        {
            DnsBestText.Text = $"{ErrorCodes.UiBenchmarkFailed}: benchmark fallido";
            NetworkText.Text = $"{ErrorCodes.UiBenchmarkFailed}: El benchmark DNS no pudo completarse. [Técnico: {ex.GetType().Name}]";
            App.WriteCrashLog(ex);
        }
    }

    private void RenderDnsBars(IReadOnlyList<DnsBenchmarkResult> results)
    {
        try
        {
            DnsBarsPanel.Children.Clear();
            var ranked = results
                .Where(r => r.MedianLatencyMs is not null)
                .OrderBy(r => r.MedianLatencyMs)
                .Take(4)
                .ToList();
            _lastDnsRows = ranked.Select(r => new DnsRowSnapshot(r.Resolver, r.MedianLatencyMs!.Value)).ToList();
            if (ranked.Count == 0) return;
            var max = ranked.Max(r => r.MedianLatencyMs!.Value);
            if (max <= 0) max = 1;
            foreach (var r in ranked)
            {
                AddBarRow(DnsBarsPanel, r.Resolver, r.MedianLatencyMs!.Value, max, $"{r.MedianLatencyMs:0.0} ms");
            }
            PersistDisplaySnapshot();
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
    }

    private void AddBarRow(StackPanel panel, string name, double value, double max, string suffix)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var nameBlock = new TextBlock { Text = name, FontSize = 11, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(nameBlock, 0);
        row.Children.Add(nameBlock);
        var bar = new ProgressBar { Minimum = 0, Maximum = max, Value = value, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(bar, 1);
        row.Children.Add(bar);
        var suffixBlock = new TextBlock { Text = suffix, FontSize = 11, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(suffixBlock, 2);
        row.Children.Add(suffixBlock);
        panel.Children.Add(row);
    }

    private async Task ApplyBestDnsAsync()
    {
        if (_bestDns == null) return;
        var pair = _secondDns is null ? _bestDns.Resolver : $"{_bestDns.Resolver},{_secondDns.Resolver}";
        var pairLabel = _secondDns is null
            ? $"primario {_bestDns.Resolver}"
            : $"primario {_bestDns.Resolver} y secundario {_secondDns.Resolver}";
        var confirm = new ContentDialog
        {
            Title = $"Aplicar DNS {_bestDns.Resolver}",
            Content = new TextBlock { Text = $"Se configurará {pairLabel} en la interfaz activa.\nBeneficio: -10-20 ms ping, menos jitter. El secundario responde si el primario falla. Requiere privilegios.\n¿Continuar?", TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Aplicar",
            CloseButtonText = "Cancelar",
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            DnsBestText.Text = $"Aplicando DNS {pairLabel}...";
            // Detectar interfaz activa (prioriza Ethernet/Wi-Fi física, ignora virtual/VPN)
            string iface = "Wi-Fi";
            try
            {
                var provider = AppHost.Resolve<CAO.Core.Interfaces.IDnsConfigurationProvider>();
                var candidates = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                        && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                        && n.GetIPProperties().GatewayAddresses.Count > 0
                        && !provider.IsVirtualOrVpn(n.Name))
                    .OrderBy(n => n.Name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase) ? 0 : n.Name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                    .ThenBy(n => n.Name)
                    .ToList();
                if (candidates.Count > 0) iface = candidates[0].Name;
                else
                {
                    foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        {
                            if (nic.GetIPProperties().GatewayAddresses.Count > 0) { iface = nic.Name; break; }
                        }
                    }
                }
            }
            catch
            {
                try
                {
                    foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        {
                            if (nic.GetIPProperties().GatewayAddresses.Count > 0) { iface = nic.Name; break; }
                        }
                    }
                } catch { }
            }
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var resp = await pipe.SetDnsAsync(iface, pair, cts.Token);
            if (resp is { Accepted: true })
            {
                DnsBestText.Text = $"✓ DNS {pairLabel} aplicado a {iface} — verificado";
                var ok = new ContentDialog { Title = Localizer.Get("dns.applied"), Content = new TextBlock { Text = $"{Localizer.Get("dns.applied")} {iface}\n{Localizer.Get("dns.primary")}: {pair}\n{Localizer.Get("dns.verified")}", TextWrapping = TextWrapping.Wrap }, CloseButtonText = "Aceptar", XamlRoot = Content.XamlRoot };
                await ok.ShowAsync();
            }
            else
            {
                var fallback = new ContentDialog { Title = Localizer.Get("dns.failed"), Content = new TextBlock { Text = $"{Localizer.Get("dns.failed")} [{resp?.ErrorCode}]: {resp?.SafeMessage}", TextWrapping = TextWrapping.Wrap }, CloseButtonText = "Aceptar", XamlRoot = Content.XamlRoot };
                await fallback.ShowAsync();
                DnsBestText.Text = $"{Localizer.Get("dns.failed")} — {resp?.ErrorCode}";
            }
        }
        catch (Exception ex)
        {
            DnsBestText.Text = $"{Localizer.Get("dns.failed")}: {ex.Message}";
            var err = new ContentDialog { Title = Localizer.Get("dns.failed"), Content = new TextBlock { Text = $"{Localizer.Get("dns.failed")}\n{ex.Message}", TextWrapping = TextWrapping.Wrap }, CloseButtonText = "Aceptar", XamlRoot = Content.XamlRoot };
            await err.ShowAsync();
        }
    }

    private async void OnDpcSampleClick(object sender, RoutedEventArgs e)
    {
        // El muestreo es independiente del análisis: si aún no hay resultados, mostrarlos.
        _forceResults = true;
        UpdateResultsVisibility();
        await RunDpcAuto(CancellationToken.None);
    }

    private void RenderGamingScan()
    {
        try
        {
            var detail = $"{_gamingVm.Status} Bloqueadas: {_gamingVm.BlockedCount} · Permitidas: {_gamingVm.AllowedCount} · Revisión: {_gamingVm.ReviewCount}.";
            if (!string.IsNullOrWhiteSpace(_gamingVm.VendorGuidance))
                detail += $"\n{_gamingVm.VendorGuidance}";
            GamingScanText.Text = detail;
        }
        catch { GamingScanText.Text = "Escaneo gaming no disponible."; }
        PersistDisplaySnapshot();
    }

    private AnalysisDisplaySnapshot BuildDisplaySnapshot() => new(
        Network: NullIfEmpty(NetworkText.Text),
        Security: NullIfEmpty(SecurityText.Text),
        Storage: NullIfEmpty(StorageText.Text),
        Drivers: NullIfEmpty(DriversText.Text),
        Input: NullIfEmpty(DiagnInputText.Text),
        Thermal: NullIfEmpty(DiagnThermalText.Text),
        Perf: NullIfEmpty(DiagnPerfText.Text),
        DnsBest: NullIfEmpty(DnsBestText.Text),
        DnsPrimary: _bestDns?.Resolver,
        DnsPrimaryMs: _bestDns?.MedianLatencyMs,
        DnsSecondary: _secondDns?.Resolver,
        DnsSecondaryMs: _secondDns?.MedianLatencyMs,
        DnsRows: _lastDnsRows,
        StorageRows: _lastStorageRows,
        Interrupts: NullIfEmpty(InterruptsText.Text),
        DpcStatus: NullIfEmpty(DpcStatusText.Text),
        DpcMax: _lastDpcMax,
        GamingScan: NullIfEmpty(GamingScanText.Text),
        GamingBlocked: NullIfEmpty(GamingBlockedText.Text),
        GamingGames: NullIfEmpty(GamingGamesText.Text));

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private void PersistDisplaySnapshot()
    {
        try
        {
            var uiState = AppHost.Resolve<ViewModels.UiState>();
            uiState.DisplaySnapshot = BuildDisplaySnapshot();
            var session = AppHost.Resolve<CAO.Infrastructure.Persistence.AnalysisSessionService>();
            var persisted = session.GetLastAnalysis();
            if (persisted is null) return;
            session.Save(persisted with
            {
                Display = uiState.DisplaySnapshot,
                Health = _viewModel.Health ?? persisted.Health,
            });
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
    }

    private static void SetRestored(TextBlock target, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) target.Text = value;
    }

    private void RestoreDisplaySnapshot()
    {
        var snap = AppHost.Resolve<ViewModels.UiState>().DisplaySnapshot;
        if (snap is null) return;
        SetRestored(NetworkText, snap.Network);
        SetRestored(SecurityText, snap.Security);
        SetRestored(StorageText, snap.Storage);
        SetRestored(DriversText, snap.Drivers);
        SetRestored(DiagnInputText, snap.Input);
        SetRestored(DiagnThermalText, snap.Thermal);
        SetRestored(DiagnPerfText, snap.Perf);
        SetRestored(DnsBestText, snap.DnsBest);
        SetRestored(InterruptsText, snap.Interrupts);
        SetRestored(DpcStatusText, snap.DpcStatus);
        SetRestored(GamingScanText, snap.GamingScan);
        SetRestored(GamingBlockedText, snap.GamingBlocked);
        SetRestored(GamingGamesText, snap.GamingGames);
        try
        {
            DnsBarsPanel.Children.Clear();
            if (snap.DnsRows?.Count > 0)
            {
                var max = snap.DnsRows.Max(r => r.Ms);
                if (max <= 0) max = 1;
                foreach (var r in snap.DnsRows)
                    AddBarRow(DnsBarsPanel, r.Resolver, r.Ms, max, $"{r.Ms:0.0} ms");
            }
            StorageBarsPanel.Children.Clear();
            if (snap.StorageRows?.Count > 0)
            {
                foreach (var r in snap.StorageRows)
                    AddBarRow(StorageBarsPanel, r.Name, r.UsedPct, 100, $"{r.FreeGb:0} GB libres");
            }
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
        if (_bestDns is null && snap.DnsPrimary is not null)
            _bestDns = new DnsBenchmarkResult(snap.DnsPrimary, snap.DnsPrimaryMs, null, 4, 4, 0);
        if (_secondDns is null && snap.DnsSecondary is not null)
            _secondDns = new DnsBenchmarkResult(snap.DnsSecondary, snap.DnsSecondaryMs, null, 4, 4, 0);
        if (snap.DpcMax is double dpc) MaybeAddDpcFinding(dpc);
        CollapseDataCards();
        UpdateResultsVisibility();
    }

    private void MaybeAddDpcFinding(double maxPercent)
    {
        if (maxPercent < 10) return;
        try
        {
            var uiState = AppHost.Resolve<ViewModels.UiState>();
            var health = _viewModel.Health
                ?? (uiState.Context is null ? null : HealthEngine.Evaluate(uiState.Context));
            if (health is null) return;
            if (!health.Findings.Any(f => f.Code == "dpc-pressure"))
            {
                var finding = new DiagnosticFinding(HealthDimension.Input, DiagnosticSeverity.Warning, "dpc-pressure",
                    $"Presión DPC elevada ({maxPercent:0.00}%): investigue drivers USB/red/audio.");
                health = new SystemDiagnosticReport(health.Scores, health.Findings.Append(finding).ToList());
            }
            _viewModel.Health = health;
            RenderHealth();
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
    }

    private async Task RunDpcAuto(CancellationToken ct)
    {
        try
        {
            DpcRing.IsActive = true;
            DpcStatusText.Text = "Muestreando 5 s...";
            InterruptsText.Text = "Muestreando interrupciones durante 5 s… Alto DPC = audio entrecortado, stutter y +5-15 ms input lag. Se mide % Tiempo DPC / % Tiempo Interrupción vía contadores.";
            var report = await new DpcLatencySampler().SampleAsync(null, ct);
            InterruptsText.Text =
                $"Ventana: {report.Window.TotalSeconds:0}s — severidad: {report.SeverityEs}\n" +
                $"% DPC máx (_Total): {report.TotalMaxDpcPercent:0.00} | % Interrupción máx (_Total): {report.TotalMaxInterruptPercent:0.00}\n" +
                $"Interpretación: {(report.TotalMaxDpcPercent > 5 ? "Alto — posible driver con latencia, revisa drivers de red/audio/GPU." : "Normal — sin impacto en juegos.")}\n" +
                "La atribución exacta por driver requiere trazas ETW (no incluida); esta medida indica severidad y si hay problema.";
            DpcStatusText.Text = $"Severidad: {report.SeverityEs}";
            _lastDpcMax = report.TotalMaxDpcPercent;
            MaybeAddDpcFinding(report.TotalMaxDpcPercent);
            PersistDisplaySnapshot();
        }
        catch (OperationCanceledException) { DpcStatusText.Text = "Cancelado"; }
        catch (Exception ex)
        {
            InterruptsText.Text = $"{ErrorCodes.UiDiagnosticsFailed}: El muestreo DPC/ISR falló. Cierre otras cargas y reintente. [Técnico: {ex.GetType().Name}]";
            DpcStatusText.Text = "Fallo";
            App.WriteCrashLog(ex);
        }
        finally { DpcRing.IsActive = false; }
    }
}
