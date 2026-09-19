using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CAO.Infrastructure.Benchmarking;
using CAO.Infrastructure.Networking;
using CAO.Shared;
using CAO.UI.Helpers;

namespace CAO.UI.Pages;

/// <summary>
/// Benchmark page (spec 66-70, 107-108): baseline vs after with an explicit
/// noise floor. If the delta is insignificant the verdict says so and the
/// change should not be kept just because "it is an optimization".
/// </summary>
public sealed partial class BenchmarkPage : Page
{
    private readonly ViewModels.BenchmarkViewModel _vm;
    private readonly ViewModels.UiState _uiState;
    private CancellationTokenSource? _runCts;
    private string? _autoStartedFor;

    public BenchmarkPage()
    {
        InitializeComponent();
        _vm = AppHost.Resolve<ViewModels.BenchmarkViewModel>();
        _uiState = AppHost.Resolve<ViewModels.UiState>();
        DataContext = _vm;
        ApplyTexts();
        _uiState.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.BenchmarkViewModel.BaselineSummary) or nameof(ViewModels.BenchmarkViewModel.ComparisonSummary) or nameof(ViewModels.BenchmarkViewModel.CurrentStep) or nameof(ViewModels.BenchmarkViewModel.Verdict) or nameof(ViewModels.BenchmarkViewModel.ContextNote) or nameof(ViewModels.BenchmarkViewModel.DnsSummary) or nameof(ViewModels.BenchmarkViewModel.FluencySummary) or nameof(ViewModels.BenchmarkViewModel.BootSummary))
                DispatcherQueue.TryEnqueue(RenderVm);
        };
    }

    private void ApplyTexts()
    {
        BaselineButton.Content = Localizer.Get("benchmark.baseline");
        AfterButton.Content = Localizer.Get("benchmark.after");
        ExportCsvButton.Content = Localizer.Get("benchmark.exportCsv");
        DnsTitleText.Text = Localizer.Get("benchmark.dnsTitle");
        BootTitleText.Text = Localizer.Get("benchmark.bootTitle");
        FluencyTitleText.Text = Localizer.Get("benchmark.fluencyTitle");
        try { LocalizationHelper.LocalizeTree(this.Content as DependencyObject ?? this); } catch { }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyTexts();
        UiAnimations.PlayEntrance(PageContent);
        FillBoot();
        var pending = _uiState.PendingBenchmarkOptimizationId;
        if (!string.IsNullOrWhiteSpace(pending))
        {
            CtxOptText.Text = $"{Localizer.Get("benchmark.ctxOpt")} {pending}";
            OptContextCard.Visibility = Visibility.Visible;
            if (!string.Equals(_autoStartedFor, pending, StringComparison.Ordinal))
            {
                _autoStartedFor = pending;
                _ = RunWithVmAsync(isBaseline: true, BaselineButton);
            }
        }
        else
        {
            OptContextCard.Visibility = Visibility.Collapsed;
        }
        _ = RepaintComparisonAsync();
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        try { _runCts?.Cancel(); } catch { }
        // El pendiente sobrevive a la navegación para poder volver.
    }

    private void RenderVm()
    {
        BaselineText.Text = _vm.BaselineSummary;
        ComparisonText.Text = _vm.ComparisonSummary;
        if (BenchStatusText is not null) BenchStatusText.Text = _vm.Status;
        if (CurrentStepText is not null) CurrentStepText.Text = _vm.CurrentStep;
        if (VerdictText is not null) VerdictText.Text = _vm.Verdict;
        if (ContextText is not null && !string.IsNullOrWhiteSpace(_vm.ContextNote)) ContextText.Text = _vm.ContextNote;
        if (StepCard is not null) StepCard.Visibility = string.IsNullOrWhiteSpace(_vm.CurrentStep) ? Visibility.Collapsed : Visibility.Visible;
        if (!string.IsNullOrWhiteSpace(_vm.DnsSummary)) DnsResultText.Text = _vm.DnsSummary;
        if (!string.IsNullOrWhiteSpace(_vm.FluencySummary)) FluencyText.Text = _vm.FluencySummary;
        if (!string.IsNullOrWhiteSpace(_vm.BootSummary)) BootText.Text = _vm.BootSummary;
    }

    /// <summary>Cambia el mood de BenchCat. Nunca lanza (constraint Plan 01).</summary>
    private void SetCat(string mood)
    {
        try { BenchCat.SetMood(mood); } catch { }
    }

    private void FillBoot()
    {
        try
        {
            var boot = BootInfoProvider.GetBootInfo();
            if (boot is null)
            {
                BootText.Text = Localizer.Get("benchmark.unavailable");
            }
            else
            {
                var local = boot.BootTimeUtc.ToLocalTime();
                BootText.Text = $"Último arranque: {local:g} · En actividad: {FormatUptime(boot.Uptime)}";
            }
            _vm.BootSummary = BootText.Text;
        }
        catch
        {
            BootText.Text = Localizer.Get("benchmark.unavailable");
        }
    }

    private static string FormatUptime(TimeSpan uptime) =>
        uptime.TotalDays >= 1
            ? $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m"
            : $"{uptime.Hours}h {uptime.Minutes}m";

    private void OnClearContextClick(object sender, RoutedEventArgs e)
    {
        _uiState.PendingBenchmarkOptimizationId = string.Empty;
        _uiState.PendingBenchmarkCategory = string.Empty;
        OptContextCard.Visibility = Visibility.Collapsed;
    }

    private void OnGoOptimizeClick(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Current is not null) MainWindow.Current.SelectRoute("optimize");
        else AppHost.Resolve<Navigation.INavigationService>().Select("optimize");
    }

    private void OnGoCleanupClick(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Current is not null) MainWindow.Current.SelectRoute("cleanup");
        else AppHost.Resolve<Navigation.INavigationService>().Select("cleanup");
    }

    private async void OnBaselineClick(object sender, RoutedEventArgs e) => await RunWithVmAsync(true, BaselineButton);
    private async void OnAfterClick(object sender, RoutedEventArgs e) => await RunWithVmAsync(false, AfterButton);

    private async Task RunWithVmAsync(bool isBaseline, Button button)
    {
        var previous = button.Content;
        button.IsEnabled = false;
        AfterButton.IsEnabled = false;
        BaselineButton.IsEnabled = false;
        Ring.IsActive = true;
        if (BenchStatusText is not null) BenchStatusText.Text = Localizer.Get("benchmark.measuring");
        SetCat("Working");
        try
        {
            try { _runCts?.Cancel(); _runCts?.Dispose(); } catch { }
            _runCts = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_runCts.Token);
            linked.CancelAfter(BenchmarkStore.TimeoutForSystemDrive());
            var optId = string.IsNullOrWhiteSpace(_uiState.PendingBenchmarkOptimizationId)
                ? "manual" : _uiState.PendingBenchmarkOptimizationId;
            var category = _uiState.PendingBenchmarkCategory ?? string.Empty;
            await _vm.RunForOptimizationAsync(optId, category, isBaseline, linked.Token);
            RenderVm();
            await RepaintComparisonAsync();
            var verdict = _vm.Verdict ?? string.Empty;
            SetCat(verdict.Contains("Mejora", StringComparison.OrdinalIgnoreCase) ? "Celebrate"
                : verdict.Contains("Regresión", StringComparison.OrdinalIgnoreCase) || verdict.Contains("Error", StringComparison.OrdinalIgnoreCase) ? "Warn"
                : "Idle");
        }
        catch (Exception ex)
        {
            ComparisonText.Text = $"{ErrorCodes.UiBenchmarkFailed}: El benchmark no pudo completarse. [Técnico: {ex.GetType().Name}]";
            SetCat("Idle");
            App.WriteCrashLog(ex);
        }
        finally
        {
            Ring.IsActive = false;
            button.Content = previous;
            button.IsEnabled = true;
            AfterButton.IsEnabled = true;
            BaselineButton.IsEnabled = true;
            if (BenchStatusText is not null && BenchStatusText.Text == Localizer.Get("benchmark.measuring")) BenchStatusText.Text = _vm.Status;
        }
    }

    /// <summary>Repinta tabla de deltas + sparkline desde la última sesión guardada. Nunca lanza.</summary>
    private async Task RepaintComparisonAsync()
    {
        try
        {
            var path = _vm.LastSessionPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                DeltaTable.Visibility = Visibility.Collapsed;
                SparkPanel.Visibility = Visibility.Collapsed;
                ExportCsvButton.Visibility = Visibility.Collapsed;
                return;
            }
            var session = await BenchmarkStore.LoadJsonAsync<BenchmarkSession>(path, CancellationToken.None);
            if (session is null)
            {
                DeltaTable.Visibility = Visibility.Collapsed;
                SparkPanel.Visibility = Visibility.Collapsed;
                ExportCsvButton.Visibility = Visibility.Collapsed;
                return;
            }
            var hasAfter = session.After is not null;
            FillDeltaRow(CpuBeforeText, CpuAfterText, CpuDeltaText, session.Before.CpuScore, session.After?.CpuScore);
            FillDeltaRow(MemBeforeText, MemAfterText, MemDeltaText, session.Before.MemoryBandwidthGbs, session.After?.MemoryBandwidthGbs);
            FillDeltaRow(DiskRBeforeText, DiskRAfterText, DiskRDeltaText, session.Before.DiskReadMbs, session.After?.DiskReadMbs);
            FillDeltaRow(DiskWBeforeText, DiskWAfterText, DiskWDeltaText, session.Before.DiskWriteMbs, session.After?.DiskWriteMbs);
            DeltaTable.Visibility = Visibility.Visible;
            ExportCsvButton.Visibility = Visibility.Visible;
            if (hasAfter)
            {
                SetSpark(CpuSpark, session.Before.CpuScore, session.After!.CpuScore);
                SetSpark(DiskSpark, session.Before.DiskReadMbs, session.After.DiskReadMbs);
                SparkPanel.Visibility = Visibility.Visible;
            }
            else
            {
                SparkPanel.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
    }

    private static void FillDeltaRow(TextBlock before, TextBlock after, TextBlock delta, double beforeValue, double? afterValue)
    {
        before.Text = beforeValue.ToString("0.##");
        if (afterValue is null)
        {
            after.Text = "—";
            delta.Text = "—";
            return;
        }
        after.Text = afterValue.Value.ToString("0.##");
        var pct = SystemBenchmarkRunner.PercentChange(beforeValue, afterValue.Value);
        delta.Text = $"{pct:+0.0;-0.0}%";
    }

    /// <summary>Sparkline honesta de 2 puntos (antes→después, medianas de la sesión). Sin trials por punto: la sesión solo guarda medianas.</summary>
    private static void SetSpark(Microsoft.UI.Xaml.Shapes.Polyline line, double before, double after)
    {
        try
        {
            const double w = 120, h = 36, pad = 4;
            var lo = Math.Min(before, after);
            var hi = Math.Max(before, after);
            var span = hi - lo;
            double Y(double v) => span <= 0 ? h / 2 : h - pad - ((v - lo) / span) * (h - 2 * pad);
            line.Points = new PointCollection
            {
                new Windows.Foundation.Point(pad, Y(before)),
                new Windows.Foundation.Point(w - pad, Y(after)),
            };
        }
        catch { }
    }

    private async void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _vm.LastSessionPath;
            if (string.IsNullOrWhiteSpace(path)) return;
            var session = await BenchmarkStore.LoadJsonAsync<BenchmarkSession>(path, CancellationToken.None);
            if (session is null) return;
            var csv = _vm.ExportSessionCsv(session);
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var saved = await TryPickSaveFileAsync($"cao-benchmark-{stamp}", csv);
            if (saved is null)
            {
                var fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"cao-benchmark-{stamp}.csv");
                await File.WriteAllTextAsync(fallback, csv);
                saved = fallback;
            }
            ExportPathText.Text = saved;
            ExportPathText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ExportPathText.Text = $"{ErrorCodes.UiBenchmarkFailed}: no se pudo exportar. [Técnico: {ex.GetType().Name}]";
            ExportPathText.Visibility = Visibility.Visible;
            App.WriteCrashLog(ex);
        }
    }

    /// <summary>FileSavePicker con el mismo interop que DriversPage; null si se cancela o falla (fallback a Documentos).</summary>
    private async Task<string?> TryPickSaveFileAsync(string suggestedName, string content)
    {
        try
        {
            var window = MainWindow.Current;
            if (window is null) return null;
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            picker.SuggestedFileName = suggestedName;
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            var file = await picker.PickSaveFileAsync();
            if (file is null) return null;
            await File.WriteAllTextAsync(file.Path, content);
            return file.Path;
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            return null;
        }
    }

    private async void OnDnsClick(object sender, RoutedEventArgs e)
    {
        DnsButton.IsEnabled = false;
        DnsResultText.Text = Localizer.Get("benchmark.measuring");
        try
        {
            try { _runCts?.Cancel(); _runCts?.Dispose(); } catch { }
            _runCts = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_runCts.Token);
            linked.CancelAfter(TimeSpan.FromSeconds(60));
            await _vm.MeasureDnsAsync(linked.Token);
            DnsResultText.Text = _vm.DnsSummary;
            RenderDnsBars(_vm.DnsLastResults);
        }
        catch (Exception ex)
        {
            DnsResultText.Text = $"{ErrorCodes.UiBenchmarkFailed}: DNS no medido. [Técnico: {ex.GetType().Name}]";
            App.WriteCrashLog(ex);
        }
        finally { DnsButton.IsEnabled = true; }
    }

    /// <summary>Barras DNS top-4 por mediana (TextBlock + Rectangle, cap 160 px). Referencia: Analyze RenderDnsBars.</summary>
    private void RenderDnsBars(IReadOnlyList<DnsBenchmarkResult>? results)
    {
        try
        {
            DnsBarsPanel.Children.Clear();
            if (results is null) return;
            var ranked = results
                .Where(r => r.MedianLatencyMs is not null)
                .OrderBy(r => r.MedianLatencyMs)
                .Take(4)
                .ToList();
            if (ranked.Count == 0) return;
            var max = ranked.Max(r => r.MedianLatencyMs!.Value);
            if (max <= 0) max = 1;
            Brush fill;
            try { fill = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]; }
            catch { fill = new SolidColorBrush(Microsoft.UI.Colors.SteelBlue); }
            foreach (var r in ranked)
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock { Text = r.Resolver, FontSize = 11, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(name, 0);
                row.Children.Add(name);
                var bar = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Fill = fill,
                    Height = 10,
                    Width = Math.Min(160, r.MedianLatencyMs!.Value / max * 160),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    RadiusX = 3,
                    RadiusY = 3,
                };
                Grid.SetColumn(bar, 1);
                row.Children.Add(bar);
                var suffix = new TextBlock { Text = $"{r.MedianLatencyMs:0.0} ms", FontSize = 11, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(suffix, 2);
                row.Children.Add(suffix);
                DnsBarsPanel.Children.Add(row);
            }
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
    }

    private async void OnFluencyClick(object sender, RoutedEventArgs e)
    {
        FluencyButton.IsEnabled = false;
        FluencyRing.IsActive = true;
        FluencyText.Text = Localizer.Get("benchmark.measuring");
        try
        {
            try { _runCts?.Cancel(); _runCts?.Dispose(); } catch { }
            _runCts = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_runCts.Token);
            linked.CancelAfter(TimeSpan.FromSeconds(30));
            await _vm.MeasureFluencyAsync(new DxgiFrameCapture(), linked.Token);
            FluencyText.Text = _vm.FluencySummary;
        }
        catch (Exception ex)
        {
            FluencyText.Text = $"{ErrorCodes.UiBenchmarkFailed}: fluidez no medida. [Técnico: {ex.GetType().Name}]";
            App.WriteCrashLog(ex);
        }
        finally
        {
            FluencyRing.IsActive = false;
            FluencyButton.IsEnabled = true;
        }
    }
}
