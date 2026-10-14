using System.Net;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CAO.Core.GameNetwork;
using CAO.Infrastructure.Networking;
using CAO.Shared;
using CAO.UI.Controls;
using CAO.UI.Helpers;

namespace CAO.UI.Pages;

public sealed record NetworkRegionRow(
    string RegionId, string Name, string Area, string PingText, string JitterText,
    string LossText, string GradeText, Brush GradeBrush, double SortKey);

public sealed record RouteHopRow(string TtlText, string AddressText, string PingText, string LossText);

/// <summary>
/// Red gaming: ping/jitter/pérdida por región de juego, monitor en vivo,
/// traceroute con diagnóstico de dónde nace el lag y aplicación de los
/// ajustes de red del catálogo. Las mediciones corren en la UI sin privilegios
/// (no cambian nada); los ajustes cruzan al servicio como cualquier otra
/// optimización (snapshot + verificación + revert).
/// </summary>
public sealed partial class NetworkPage : Page
{
    private const int RegionSamples = 10;
    private const int LiveWindow = 60;
    private const string AllAreas = "Todas las zonas";

    private readonly List<double?> _liveSamples = [];
    private List<NetworkRegionRow> _regionRows = [];
    private CancellationTokenSource? _liveCts;
    private CancellationTokenSource? _measureCts;
    private bool _busy;

    public NetworkPage()
    {
        try { InitializeComponent(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"NetworkPage init failed: {ex}"); throw; }
    }

    private void ApplyPageTourText()
    {
        try
        {
            PageTourButton.Content = Localizer.Get("tour.page.button");
            ToolTipService.SetToolTip(PageTourButton, Localizer.Get("tour.page.tip"));
        }
        catch { }
    }

    private void OnPageTourClick(object sender, RoutedEventArgs e)
    {
        if (Tutorial.TourCoordinator.IsActive) return;
        MainWindow.Current?.StartPageTour("network");
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyPageTourText();
        UiAnimations.PlayEntrance(PageContent);
        if (AreaCombo.Items.Count == 0)
        {
            AreaCombo.Items.Add(AllAreas);
            foreach (var area in GameRegionCatalog.All.Select(r => r.Area).Distinct()) AreaCombo.Items.Add(area);
            AreaCombo.SelectedIndex = 0;
        }
        ConnectionText.Text = GameRouteProbe.ActiveLinkDescription();
        WifiWarningText.Visibility = GameRouteProbe.IsWifiActive() ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopLive();
        try { _measureCts?.Cancel(); } catch { }
    }

    // ---------- Regiones ----------

    private async void OnMeasureRegionsClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        MeasureRegionsButton.IsEnabled = false;
        RegionsProgress.Visibility = Visibility.Visible;
        RegionsProgress.Value = 0;
        Mascot.Set("Working");
        _measureCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = _measureCts.Token;
        try
        {
            var regions = RegionsForSelectedArea();
            var done = 0;
            var rows = new List<NetworkRegionRow>();
            // 4 regiones a la vez: suficiente para acabar rápido sin que las
            // propias mediciones compitan por el enlace y se ensucien.
            using var gate = new SemaphoreSlim(4);
            var tasks = regions.Select(async region =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var stats = await MeasureRegionAsync(region, ct);
                    lock (rows) rows.Add(ToRow(region, stats));
                }
                finally
                {
                    gate.Release();
                    var finished = Interlocked.Increment(ref done);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        RegionsProgress.Value = finished * 100.0 / regions.Count;
                        RegionsStatusText.Text = $"Midiendo… {finished}/{regions.Count} regiones.";
                    });
                }
            }).ToList();
            await Task.WhenAll(tasks);

            _regionRows = rows.OrderBy(r => r.SortKey).ToList();
            RegionsList.ItemsSource = _regionRows;
            var best = _regionRows.FirstOrDefault(r => r.SortKey < double.MaxValue);
            RegionsStatusText.Text = best is null
                ? "Ninguna región respondió. Revisa la conexión o el firewall."
                : $"Mejor región: {best.Name} ({best.PingText}, jitter {best.JitterText}). Elige esa región o la más cercana en el juego.";
            if (best is not null) Mascot.CelebrateThenIdle(DispatcherQueue); else Mascot.Set("Warn");
        }
        catch (OperationCanceledException)
        {
            RegionsStatusText.Text = "Medición cancelada.";
            Mascot.Set("Idle");
        }
        catch (Exception ex)
        {
            RegionsStatusText.Text = $"No se pudo medir: {ex.Message}";
            Mascot.Set("Warn");
            App.WriteCrashLog(ex);
        }
        finally
        {
            RegionsProgress.Visibility = Visibility.Collapsed;
            MeasureRegionsButton.IsEnabled = true;
            _busy = false;
        }
    }

    private static async Task<LatencyStats> MeasureRegionAsync(GameRegion region, CancellationToken ct)
    {
        var address = await GameRouteProbe.ResolveAsync(region.Host, ct);
        if (address is null) return LatencyStats.From(Enumerable.Repeat<double?>(null, RegionSamples).ToList());
        return await GameRouteProbe.MeasureAsync(address, region.Port, RegionSamples, 150, ct);
    }

    private List<GameRegion> RegionsForSelectedArea()
    {
        var area = AreaCombo.SelectedItem as string;
        return area is null || area == AllAreas
            ? GameRegionCatalog.All.ToList()
            : GameRegionCatalog.All.Where(r => r.Area == area).ToList();
    }

    private void OnAreaChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_regionRows.Count == 0) return;
        var area = AreaCombo.SelectedItem as string;
        RegionsList.ItemsSource = area is null || area == AllAreas
            ? _regionRows
            : _regionRows.Where(r => r.Area == area).ToList();
    }

    private void OnRegionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (RegionsList.SelectedItem is not NetworkRegionRow row) return;
        var region = GameRegionCatalog.Find(row.RegionId);
        if (region is null) return;
        TargetBox.Text = region.Host;
        LiveStatusText.Text = $"Destino: {region.NameEs}. Pulsa Iniciar para vigilarlo o Analizar ruta.";
    }

    private static NetworkRegionRow ToRow(GameRegion region, LatencyStats stats)
    {
        var grade = stats.Grade;
        return new NetworkRegionRow(
            region.Id, region.NameEs, region.Area,
            Ms(stats.MedianMs), Ms(stats.JitterMs), $"{stats.LossPercent:0.#} %",
            LatencyStats.GradeLabelEs(grade), BrushFor(grade),
            stats.MedianMs ?? double.MaxValue);
    }

    // ---------- Monitor en vivo ----------

    private async void OnLiveToggleClick(object sender, RoutedEventArgs e)
    {
        if (_liveCts is not null)
        {
            StopLive();
            return;
        }

        if (!GameRegionCatalog.TryParseCustomTarget(TargetBox.Text, out var host, out var port))
        {
            LiveStatusText.Text = "Escribe una IP o host válido (opcional :puerto), o selecciona una región medida.";
            return;
        }

        _liveCts = new CancellationTokenSource();
        var ct = _liveCts.Token;
        LiveToggleButton.Content = "Detener";
        _liveSamples.Clear();
        RedrawChart();
        LiveStatusText.Text = $"Resolviendo {host}…";
        try
        {
            var address = await GameRouteProbe.ResolveAsync(host, ct);
            if (address is null)
            {
                LiveStatusText.Text = $"No se pudo resolver {host}.";
                StopLive();
                return;
            }

            LiveStatusText.Text = $"Midiendo {address}:{port} cada segundo. Deja la página abierta mientras juegas.";
            var spikes = 0;
            while (!ct.IsCancellationRequested)
            {
                var started = DateTime.UtcNow;
                var sample = await GameRouteProbe.TcpRttAsync(address, port, ct);
                _liveSamples.Add(sample);
                if (_liveSamples.Count > LiveWindow) _liveSamples.RemoveAt(0);

                var stats = LatencyStats.From(_liveSamples);
                if (sample is { } value && stats.MedianMs is { } median && value > median * 1.5 + 20) spikes++;
                LivePingText.Text = Ms(sample);
                LiveJitterText.Text = Ms(stats.JitterMs);
                LiveLossText.Text = $"{stats.LossPercent:0.#} %";
                LiveGradeText.Text = LatencyStats.GradeLabelEs(stats.Grade);
                LiveGradeText.Foreground = BrushFor(stats.Grade);
                LiveStatusText.Text = $"{address}:{port} · mediana {Ms(stats.MedianMs)} · p95 {Ms(stats.P95Ms)} · picos {spikes} · últimos {_liveSamples.Count} s";
                RedrawChart();

                var wait = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - started);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Detenido por el usuario o al salir de la página.
        }
        catch (Exception ex)
        {
            LiveStatusText.Text = $"Monitor detenido: {ex.Message}";
            App.WriteCrashLog(ex);
            StopLive();
        }
    }

    private void StopLive()
    {
        try { _liveCts?.Cancel(); } catch { }
        _liveCts?.Dispose();
        _liveCts = null;
        try { LiveToggleButton.Content = "Iniciar"; } catch { }
    }

    private void OnChartSizeChanged(object sender, SizeChangedEventArgs e) => RedrawChart();

    /// <summary>Dibuja la ventana de muestras; una pérdida se pinta arriba del todo para que se vea.</summary>
    private void RedrawChart()
    {
        try
        {
            var width = LiveChartCanvas.ActualWidth;
            var height = LiveChartCanvas.ActualHeight;
            var points = new PointCollection();
            if (width <= 0 || height <= 0 || _liveSamples.Count == 0)
            {
                LiveChartLine.Points = points;
                return;
            }
            var received = _liveSamples.Where(s => s.HasValue).Select(s => s!.Value).ToList();
            var top = Math.Max(50, (received.Count == 0 ? 50 : received.Max()) * 1.2);
            var step = width / (LiveWindow - 1);
            var offset = LiveWindow - _liveSamples.Count;
            for (var i = 0; i < _liveSamples.Count; i++)
            {
                var value = _liveSamples[i] ?? top;
                var y = height - Math.Clamp(value / top, 0, 1) * height;
                points.Add(new Windows.Foundation.Point((offset + i) * step, y));
            }
            LiveChartLine.Points = points;
        }
        catch
        {
            // La gráfica es decorativa: nunca debe tumbar el monitor.
        }
    }

    // ---------- Ruta ----------

    private async void OnTraceClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!GameRegionCatalog.TryParseCustomTarget(TargetBox.Text, out var host, out _))
        {
            RouteVerdictText.Text = "Selecciona una región medida o escribe una IP/host en el destino del monitor.";
            return;
        }

        _busy = true;
        TraceButton.IsEnabled = false;
        Mascot.Set("Working");
        var hops = new List<RouteHopRow>();
        HopsList.ItemsSource = null;
        RouteVerdictText.Text = $"Trazando la ruta a {host}…";
        _measureCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            var address = await GameRouteProbe.ResolveAsync(host, _measureCts.Token);
            if (address is null)
            {
                RouteVerdictText.Text = $"No se pudo resolver {host}.";
                Mascot.Set("Warn");
                return;
            }

            var progress = new Progress<RouteHop>(hop =>
            {
                hops.Add(ToHopRow(hop));
                HopsList.ItemsSource = hops.ToList();
                RouteVerdictText.Text = $"Salto {hop.Ttl}…";
            });
            var result = await GameRouteProbe.TraceAsync(address, 24, 3, progress, _measureCts.Token);
            var verdict = RouteDiagnosis.Analyze(result);
            HopsList.ItemsSource = result.Select(ToHopRow).ToList();
            RouteVerdictText.Text = verdict.MessageEs;
            if (verdict.Kind is RouteVerdictKind.Healthy or RouteVerdictKind.DestinationUnreachable) Mascot.CelebrateThenIdle(DispatcherQueue);
            else Mascot.Set("Warn");
        }
        catch (OperationCanceledException)
        {
            RouteVerdictText.Text = "Análisis cancelado.";
            Mascot.Set("Idle");
        }
        catch (Exception ex)
        {
            RouteVerdictText.Text = $"No se pudo analizar la ruta: {ex.Message}";
            Mascot.Set("Warn");
            App.WriteCrashLog(ex);
        }
        finally
        {
            TraceButton.IsEnabled = true;
            _busy = false;
        }
    }

    private static RouteHopRow ToHopRow(RouteHop hop) => new(
        hop.Ttl.ToString(System.Globalization.CultureInfo.InvariantCulture),
        hop.Address ?? "* (no responde)",
        Ms(hop.Stats.MedianMs),
        hop.Stats.Received == 0 ? "—" : $"{hop.Stats.LossPercent:0.#} %");

    // ---------- Optimizar ----------

    private async void OnBoostApplyClick(object sender, RoutedEventArgs e)
    {
        var ids = BoostOptionsPanel.Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true && c.Tag is string)
            .Select(c => (string)c.Tag)
            .ToList();
        if (ids.Count == 0)
        {
            BoostStatusText.Text = "Marca al menos un ajuste.";
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Optimizar red para juegos",
            Content = $"Se aplicarán {ids.Count} ajustes de red, cada uno con snapshot y reversible desde Restaurar. Algunos requieren reiniciar. ¿Continuar?",
            PrimaryButtonText = "Aplicar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await UiDialogs.ShowAsync(dialog) != ContentDialogResult.Primary) return;

        BoostApplyButton.IsEnabled = false;
        BoostProgress.Visibility = Visibility.Visible;
        BoostProgress.Value = 0;
        Mascot.Set("Working");
        var lines = new List<string>();
        var ok = 0;
        try
        {
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                BoostStatusText.Text = $"Aplicando {id}… ({i + 1}/{ids.Count})";
                string line;
                try
                {
                    using var cts = new CancellationTokenSource(TimeoutProfile.ClientResponseDefault);
                    var response = await pipe.ApplyAsync(id, cts.Token);
                    var detail = string.IsNullOrWhiteSpace(response?.SafeMessage) ? null : response!.SafeMessage!.Trim();
                    if (response is { Accepted: true })
                    {
                        ok++;
                        line = $"✓ {id}{(detail is null ? string.Empty : ": " + detail)}";
                    }
                    else
                    {
                        line = $"✗ {id} [{response?.ErrorCode}]: {detail ?? "sin respuesta"}";
                    }
                }
                catch (Exception ex)
                {
                    line = $"✗ {id}: servicio no disponible ({ex.Message})";
                    App.WriteCrashLog(ex);
                }
                lines.Add(line);
                BoostProgress.Value = (i + 1) * 100.0 / ids.Count;
            }

            BoostStatusText.Text = $"{ok} de {ids.Count} aplicados.\n" + string.Join("\n", lines);
            if (ok == ids.Count) Mascot.CelebrateThenIdle(DispatcherQueue); else Mascot.Set("Warn");
        }
        finally
        {
            BoostProgress.Visibility = Visibility.Collapsed;
            BoostApplyButton.IsEnabled = true;
        }
    }

    private void OnOpenBenchmarkClick(object sender, RoutedEventArgs e) => MainWindow.Current?.SelectRoute("benchmark");

    // ---------- Formato ----------

    private static string Ms(double? value) => value is null ? "—" : $"{value:0} ms";

    private static Brush BrushFor(ConnectionGrade grade)
    {
        var key = grade switch
        {
            ConnectionGrade.Excellent or ConnectionGrade.Good => "SystemFillColorSuccessBrush",
            ConnectionGrade.Fair => "SystemFillColorCautionBrush",
            _ => "SystemFillColorCriticalBrush",
        };
        try
        {
            if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Brush brush) return brush;
        }
        catch { }
        return new SolidColorBrush(grade switch
        {
            ConnectionGrade.Excellent or ConnectionGrade.Good => Colors.ForestGreen,
            ConnectionGrade.Fair => Colors.DarkOrange,
            _ => Colors.IndianRed,
        });
    }
}
