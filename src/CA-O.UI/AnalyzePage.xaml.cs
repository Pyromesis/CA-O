#pragma warning disable CA2016
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
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
    private readonly AnalyzeViewModel _viewModel;
    private readonly ViewModels.DiagnosticsViewModel _diagnosticsVm;
    private CancellationTokenSource? _cts;
    private DnsBenchmarkResult? _bestDns;

    public AnalyzePage()
    {
        InitializeComponent();
        _viewModel = AppHost.Resolve<AnalyzeViewModel>();
        _diagnosticsVm = AppHost.Resolve<ViewModels.DiagnosticsViewModel>();
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
        // Hydrate VM without ResetResults if persisted analysis exists
        var session = AppHost.Resolve<CAO.Infrastructure.Persistence.AnalysisSessionService>();
        var persisted = session.GetLastAnalysis();
        if (persisted?.Context != null)
        {
            _viewModel.HydrateFromPersistedAnalysis(persisted);
            RenderFromViewModel();
        }
        ApplyTexts();
        UpdateFreshnessBanner();
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
        if (state.Context is not { } ctx) return;
        // Datos persistentes: mostrar sin necesidad de re-ejecutar análisis
        CpuText.Text = $"CPU: {ctx.CpuName} · {ctx.CpuCores} núcleos / {ctx.CpuLogicalProcessors} hilos";
        GpuText.Text = string.IsNullOrWhiteSpace(ctx.GpuName) ? "GPU: no detectada" : $"GPU: {ctx.GpuName} ({ctx.GpuVendor}) · Driver {ctx.GpuDriverVersion} · {ctx.DisplayRefreshHz} Hz {(ctx.VrrSupported ? "VRR" : "")}";
        MemoryText.Text = $"RAM: {ctx.RamGb} GB · {(ctx.HasSsd ? "SSD" : "HDD")} {(ctx.HasNvme ? "NVMe" : "")} · {(ctx.IsLaptop ? "Portátil" : "Sobremesa")} {(ctx.OnBattery ? "· Batería" : "")}";
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
    }

    private void RenderDiagnostics()
    {
        DiagnInputText.Text = string.IsNullOrWhiteSpace(_diagnosticsVm.InputSummary) ? "Ejecuta análisis para medir entrada (HID, aceleración ratón)" : _diagnosticsVm.InputSummary;
        DiagnThermalText.Text = string.IsNullOrWhiteSpace(_diagnosticsVm.ThermalSummary) ? "Ejecuta análisis para medir térmicas" : _diagnosticsVm.ThermalSummary;
        DiagnPerfText.Text = string.IsNullOrWhiteSpace(_diagnosticsVm.PerformanceSummary) ? "Ejecuta análisis para medir rendimiento" : _diagnosticsVm.PerformanceSummary;
    }

    private async void OnRunClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        RunButton.IsEnabled = false;
        CancelButton.Visibility = Visibility.Visible;
        Ring.IsActive = true;
        ProgressCard.Visibility = Visibility.Visible;
        StatusText.Text = "Midiendo…";
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
                NetStatusBadge.Background = network.Measurements.Any(m => m.MedianLatencyMs is not null) ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"] : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];
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
                NetStatusBadge.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
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
                        $"• {feature.Name}: {(feature.Enabled is null ? "desconocido" : feature.Enabled.Value ? "activado" : "desactivado")} ({feature.Evidence})")) +
                    $"\nVanguard: {(security.VanguardDetected ? "detectado" : "no detectado")}";
                SecStatusBadge.Visibility = Visibility.Visible;
                SecStatusText.Text = security.VanguardDetected ? "Anti-cheat detectado" : "Sin anti-cheat";
                SecStatusBadge.Background = security.VanguardDetected ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"] : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
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
                    if (problem.Count == 0)
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
            _bestDns = best;
            NetworkText.Text = "DNS benchmark:\n" + string.Join("\n", results.Select(result =>
                $"• {result.Resolver}: " +
                (result.MedianLatencyMs is null ? "sin respuesta" : $"{result.MedianLatencyMs:0.0} ms") +
                $", éxito {result.Successes}/{result.Attempts}")) +
                (best is null ? "\nSin datos suficientes para recomendar." : $"\nMejor medido: {best.Resolver} (recomendación basada en medición).");
            if (best != null)
            {
                DnsBestText.Text = $"Mejor DNS: {best.Resolver} ({best.MedianLatencyMs:0.0} ms) — pulsa de nuevo para aplicar";
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

    private async Task ApplyBestDnsAsync()
    {
        if (_bestDns == null) return;
        var confirm = new ContentDialog
        {
            Title = $"Aplicar DNS {_bestDns.Resolver}",
            Content = new TextBlock { Text = $"Se configurará {_bestDns.Resolver} como DNS primario en la interfaz activa.\nBeneficio: -10-20 ms ping, menos jitter. Requiere privilegios.\n¿Continuar?", TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Aplicar",
            CloseButtonText = "Cancelar",
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            DnsBestText.Text = $"Aplicando DNS {_bestDns.Resolver}...";
            // Detectar interfaz activa (primera Up con gateway)
            string iface = "Wi-Fi";
            try
            {
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        if (nic.GetIPProperties().GatewayAddresses.Count > 0) { iface = nic.Name; break; }
                    }
                }
            }
            catch { }
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var resp = await pipe.SetDnsAsync(iface, _bestDns.Resolver, cts.Token);
            if (resp is { Accepted: true })
            {
                DnsBestText.Text = $"✓ DNS {_bestDns.Resolver} aplicado a {iface} — verificado";
                var ok = new ContentDialog { Title = Localizer.Get("dns.applied"), Content = new TextBlock { Text = $"{Localizer.Get("dns.applied")} {iface}\n{Localizer.Get("dns.primary")}: {_bestDns.Resolver}\n{Localizer.Get("dns.verified")}", TextWrapping = TextWrapping.Wrap }, CloseButtonText = "Aceptar", XamlRoot = Content.XamlRoot };
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
        await RunDpcAuto(CancellationToken.None);
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
