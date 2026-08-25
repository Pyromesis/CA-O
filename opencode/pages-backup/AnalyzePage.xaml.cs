using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Networking;
using CAO.Infrastructure.Security;
using CAO.Infrastructure.Storage;
using CAO.Infrastructure.SystemInterop;

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
        try
        {
            var network = await new NetworkDiagnosticsProvider().MeasureAsync();
            NetworkText.Text = "Interfaces: " + string.Join(", ", network.Interfaces) + "\n" + string.Join("\n",
                network.Measurements.Select(measurement =>
                    $"{measurement.Kind} {measurement.Endpoint}: " +
                    (measurement.MedianLatencyMs is null ? "sin respuesta" :
                        $"{measurement.MedianLatencyMs:0.0} ms, jitter {measurement.JitterMs:0.0} ms, pérdida {measurement.Attempts - measurement.SuccessfulAttempts}/{measurement.Attempts}")));

            var security = new SecurityDiagnosticsProvider().Measure();
            SecurityText.Text = string.Join("\n", security.Features.Select(feature =>
                    $"• {feature.Name}: {(feature.Enabled is null ? "desconocido" : feature.Enabled.Value ? "activado" : "desactivado")} ({feature.Evidence})")) +
                $"\nVanguard: {(security.VanguardDetected ? "detectado" : "no detectado")}";

            var storage = new StorageDiagnosticsProvider().Measure();
            const double gib = 1024d * 1024 * 1024;
            StorageText.Text = string.Join("\n", storage.Volumes.Select(volume =>
                $"• {volume.Name} {volume.FileSystem}: libre {volume.FreeBytes / gib:0.0} de {volume.TotalBytes / gib:0.0} GB{(volume.IsSystemVolume ? " [sistema]" : "")}"));

            var drivers = await new DriverDiagnosticsProvider().MeasureAsync();
            var problems = drivers.Drivers.Count(driver => driver.ProblemCode != 0 || driver.IsSigned == false);
            DriversText.Text = $"{drivers.Drivers.Count} drivers enumerados; {problems} con código de problema o sin firma válida.";
        }
        catch (Exception ex)
        {
            NetworkText.Text = $"Fallo midiendo red: {ex.Message}";
        }
        finally
        {
            Ring.IsActive = false;
            RunButton.IsEnabled = true;
        }
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
                (best is null ? "\nSin resolver suficiente para recomendar." : $"\nMejor medido: {best.Resolver} (recomendación basada en medición).");
        }
        catch (Exception ex)
        {
            NetworkText.Text = $"DNS benchmark falló: {ex.Message}";
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
            InterruptsText.Text = $"El muestreo falló: {ex.Message}";
        }
    }
}
