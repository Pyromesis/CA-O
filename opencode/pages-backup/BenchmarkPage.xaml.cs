using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Benchmark page (spec 66-70, 107-108): baseline vs after with an explicit
/// noise floor. If the delta is insignificant the verdict says so and the
/// change should not be kept just because "it is an optimization".
/// </summary>
public sealed partial class BenchmarkPage : Page
{
    private static string BaselinePath =>
        Path.Combine(CaOPaths.BenchmarksDirectory, "baseline.json");

    public BenchmarkPage()
    {
        InitializeComponent();
        BaselineButton.Content = Localizer.Get("benchmark.baseline");
        AfterButton.Content = Localizer.Get("benchmark.after");
    }

    private async void OnBaselineClick(object sender, RoutedEventArgs e) =>
        await RunAsync(BaselineButton, isBaseline: true);

    private async void OnAfterClick(object sender, RoutedEventArgs e) =>
        await RunAsync(AfterButton, isBaseline: false);

    private async Task RunAsync(Button button, bool isBaseline)
    {
        var previous = button.Content;
        button.IsEnabled = false;
        Ring.IsActive = true;
        try
        {
            Directory.CreateDirectory(CaOPaths.BenchmarksDirectory);
            var runner = new SystemBenchmarkRunner();
            var result = await runner.RunAsync(isBaseline ? "baseline" : "after-change");

            BaselineText.Text = Describe(result);

            if (isBaseline)
            {
                await File.WriteAllTextAsync(BaselinePath, JsonSerializer.Serialize(result));
                ComparisonText.Text = "Línea base guardada; mida de nuevo tras aplicar un cambio para comparar.";
            }
            else
            {
                if (!File.Exists(BaselinePath))
                {
                    ComparisonText.Text = "No hay línea base guardada; mida primero la línea base.";
                    return;
                }

                var baseline = JsonSerializer.Deserialize<SystemBenchmarkResult>(await File.ReadAllTextAsync(BaselinePath));
                if (baseline is null)
                {
                    ComparisonText.Text = "La línea base guardada no es legible.";
                    return;
                }

                var comparison = SystemBenchmarkRunner.Compare(baseline, result);
                ComparisonText.Text =
                    $"CPU: {comparison.CpuDeltaPercent:+0.0;-0.0}% | Memoria: {comparison.MemoryDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs} " +
                    $"(suelo de ruido ±{SystemBenchmarkRunner.NoiseFloorPercent:0}%).\n" +
                    (comparison.VerdictEs == "Regresión"
                        ? "Recomendación: revertir el cambio."
                        : comparison.VerdictEs == "Sin mejora medible"
                            ? "Recomendación: sin evidencia para mantener el cambio."
                            : "Mejora medida; puede mantenerse.");
            }
        }
        catch (Exception ex)
        {
            ComparisonText.Text = $"El benchmark falló: {ex.Message}";
        }
        finally
        {
            Ring.IsActive = false;
            button.Content = previous;
            button.IsEnabled = true;
        }
    }

    private static string Describe(SystemBenchmarkResult result) =>
        $"{result.WorkloadId} @ {result.Header.TimestampUtc.ToLocalTime():g}\n" +
        $"CPU (nº de primos en carga fija): {result.CpuScore:0}\n" +
        $"Memoria: {result.MemoryBandwidthGbs:0.00} GB/s\n" +
        $"Disco secuencial: lectura {result.DiskReadMbs:0} MB/s, escritura {result.DiskWriteMbs:0} MB/s\n" +
        $"Duración total: {result.Elapsed.TotalSeconds:0.0}s";
}
