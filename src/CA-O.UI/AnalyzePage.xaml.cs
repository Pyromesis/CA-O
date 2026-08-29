using Microsoft.UI.Xaml;
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
    private CancellationTokenSource? _cts;

    public AnalyzePage()
    {
        InitializeComponent();
        _viewModel = AppHost.Resolve<AnalyzeViewModel>();
        // Opcional: exponer para binding futuro
        DataContext = _viewModel;
        Loaded += (_, _) => LoadPersisted();
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

    private void RenderFromViewModel()
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
                DriversText.Text = drv.Value ?? "Drivers medidos";
            }
            else if (drv.Status == ViewModels.AnalysisModuleStatus.Failed)
            {
                DriversText.Text = $"{ErrorCodes.UiDiagnosticsFailed}: {drv.Message}";
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
        try
        {
            var results = await new DnsBenchmarkProvider().BenchmarkAsync();
            var best = DnsBenchmarkProvider.PickBest(results);
            NetworkText.Text = "DNS benchmark:\n" + string.Join("\n", results.Select(result =>
                $"• {result.Resolver}: " +
                (result.MedianLatencyMs is null ? "sin respuesta" : $"{result.MedianLatencyMs:0.0} ms") +
                $", éxito {result.Successes}/{result.Attempts}")) +
                (best is null ? "\nSin datos suficientes para recomendar." : $"\nMejor medido: {best.Resolver} (recomendación basada en medición).");
        }
        catch (Exception ex)
        {
            NetworkText.Text = $"{ErrorCodes.UiBenchmarkFailed}: El benchmark DNS no pudo completarse. Reintente más tarde. [Técnico: {ex.GetType().Name}]";
            App.WriteCrashLog(ex);
        }
    }

    private async void OnDpcSampleClick(object sender, RoutedEventArgs e)
    {
        try
        {
            InterruptsText.Text = "Muestreando interrupciones durante 5 s…";
            var report = await new DpcLatencySampler().SampleAsync();
            InterruptsText.Text =
                $"Ventana: {report.Window.TotalSeconds:0}s — severidad: {report.SeverityEs}\n" +
                $"% DPC máx (_Total): {report.TotalMaxDpcPercent:0.00} | % Interrupción máx (_Total): {report.TotalMaxInterruptPercent:0.00}\n" +
                "La atribución por driver requiere trazas ETW; esta medida indica si existe un problema y su magnitud.";
        }
        catch (Exception ex)
        {
            InterruptsText.Text = $"{ErrorCodes.UiDiagnosticsFailed}: El muestreo DPC/ISR falló. Cierre otras cargas y reintente. [Técnico: {ex.GetType().Name}]";
            App.WriteCrashLog(ex);
        }
    }
}
