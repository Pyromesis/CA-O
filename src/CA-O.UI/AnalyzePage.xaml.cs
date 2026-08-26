using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Networking;
using CAO.Infrastructure.Security;
using CAO.Infrastructure.Storage;
using CAO.Infrastructure.SystemInterop;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>Diagnostics-first page (spec 17, 34, 51-54): measure everything, change nothing.</summary>
public sealed partial class AnalyzePage : Page
{
    public AnalyzePage()
    {
        InitializeComponent();
    }

    private async void OnRunClick(object sender, RoutedEventArgs e)
    {
        RunButton.IsEnabled = false;
        Ring.IsActive = true;
        ProgressCard.Visibility = Visibility.Visible;
        StatusText.Text = "Midiendo…";
        SetProgress(true);
        try
        {
            var network = await WithRing(NetRing, () => new NetworkDiagnosticsProvider().MeasureAsync());
            NetworkText.Text = "Interfaces: " + string.Join(", ", network.Interfaces) + "\n" + string.Join("\n",
                network.Measurements.Select(measurement =>
                    $"{measurement.Kind} {measurement.Endpoint}: " +
                    (measurement.MedianLatencyMs is null ? "sin respuesta" :
                        $"{measurement.MedianLatencyMs:0.0} ms, jitter {measurement.JitterMs:0.0} ms, pérdida {measurement.Attempts - measurement.SuccessfulAttempts}/{measurement.Attempts}")));
            NetStatusBadge.Visibility = Visibility.Visible;
            NetStatusText.Text = network.Measurements.Any(m => m.MedianLatencyMs is not null) ? "Medido" : "Sin datos";
            NetStatusBadge.Background = network.Measurements.Any(m => m.MedianLatencyMs is not null) ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"] : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];

            var security = await WithRing(SecRing, () => Task.FromResult(new SecurityDiagnosticsProvider().Measure()));
            SecurityText.Text = string.Join("\n", security.Features.Select(feature =>
                    $"• {feature.Name}: {(feature.Enabled is null ? "desconocido" : feature.Enabled.Value ? "activado" : "desactivado")} ({feature.Evidence})")) +
                $"\nVanguard: {(security.VanguardDetected ? "detectado" : "no detectado")}";
            SecStatusBadge.Visibility = Visibility.Visible;
            SecStatusText.Text = security.VanguardDetected ? "Anti-cheat detectado" : "Sin anti-cheat";
            SecStatusBadge.Background = security.VanguardDetected ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"] : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];

            const double gib = 1024d * 1024 * 1024;
            var storage = await WithRing(StorRing, () => Task.FromResult(new StorageDiagnosticsProvider().Measure()));
            StorageText.Text = string.Join("\n", storage.Volumes.Select(volume =>
                $"• {volume.Name} {volume.FileSystem}: libre {volume.FreeBytes / gib:0.0} de {volume.TotalBytes / gib:0.0} GB{(volume.IsSystemVolume ? " [sistema]" : "")}"));

            var drivers = await WithRing(DrvRing, () => new DriverDiagnosticsProvider().MeasureAsync());
            var problems = drivers.Drivers.Count(driver => driver.ProblemCode != 0 || driver.IsSigned == false);
            DriversText.Text = $"{drivers.Drivers.Count} drivers enumerados; {problems} con código de problema o sin firma válida.";
            StatusText.Text = "Análisis completo — ningún cambio aplicado.";
        }
        catch (Exception ex)
        {
            NetworkText.Text = $"{ErrorCodes.UiAnalyzeFailed}: No fue posible medir la red. Compruebe su conexión e intente de nuevo. [Técnico: {ex.GetType().Name}]";
            StatusText.Text = $"{ErrorCodes.UiAnalyzeFailed}: medición de red fallida";
            CAO.UI.App.WriteCrashLog(ex);
        }
        finally
        {
            Ring.IsActive = false;
            RunButton.IsEnabled = true;
            SetProgress(false);
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
