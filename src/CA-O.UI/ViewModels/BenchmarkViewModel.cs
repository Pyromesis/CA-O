using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para BenchmarkPage — benchmark A/B con trials y suelo de ruido explícito (§46-48).</summary>
public sealed partial class BenchmarkViewModel : ObservableObject
{
    private static string BaselinePath => Path.Combine(CaOPaths.BenchmarksDirectory, "baseline.json");

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _baselineSummary = string.Empty;
    [ObservableProperty] private string _comparisonSummary = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    public async Task RunAsync(bool isBaseline, CancellationToken ct)
    {
        IsRunning = true;
        Status = "Midiendo…";
        try
        {
            Directory.CreateDirectory(CaOPaths.BenchmarksDirectory);
            var runner = new SystemBenchmarkRunner();
            var result = await runner.RunAsync(isBaseline ? "baseline" : "after-change", ct);

            BaselineSummary = Describe(result);

            if (isBaseline)
            {
                await File.WriteAllTextAsync(BaselinePath, JsonSerializer.Serialize(result), ct);
                ComparisonSummary = "Línea base guardada; mida tras aplicar un cambio para comparar.";
            }
            else
            {
                if (!File.Exists(BaselinePath))
                {
                    ComparisonSummary = "No hay línea base guardada; mida primero la línea base.";
                    return;
                }
                var baseline = JsonSerializer.Deserialize<SystemBenchmarkResult>(await File.ReadAllTextAsync(BaselinePath, ct));
                if (baseline is null) { ComparisonSummary = "La línea base guardada no es legible."; return; }
                var comparison = SystemBenchmarkRunner.Compare(baseline, result);
                ComparisonSummary =
                    $"CPU: {comparison.CpuDeltaPercent:+0.0;-0.0}% | Memoria: {comparison.MemoryDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs} " +
                    $"(suelo ±{SystemBenchmarkRunner.NoiseFloorPercent:0}%).\n" +
                    (comparison.VerdictEs == "Regresión" ? "Recomendación: revertir." : comparison.VerdictEs == "Sin mejora medible" ? "Recomendación: sin evidencia para mantener." : "Mejora medida; puede mantenerse.");
            }
        }
        catch (OperationCanceledException) { Status = "Benchmark cancelado."; }
        catch (Exception ex)
        {
            ComparisonSummary = $"{ErrorCodes.UiBenchmarkFailed}: El benchmark no pudo completarse. [Técnico: {ex.GetType().Name}]";
            Status = $"{ErrorCodes.UiBenchmarkFailed}: benchmark fallido";
            App.WriteCrashLog(ex);
        }
        finally { IsRunning = false; if (Status == "Midiendo…") Status = string.Empty; }
    }

    [RelayCommand]
    private Task RunBaselineAsync(CancellationToken ct) => RunAsync(true, ct);

    [RelayCommand]
    private Task RunAfterAsync(CancellationToken ct) => RunAsync(false, ct);

    private static string Describe(SystemBenchmarkResult result) =>
        $"{result.WorkloadId} @ {result.Header.TimestampUtc.ToLocalTime():g}\n" +
        $"CPU: {result.CpuScore:0} | Memoria: {result.MemoryBandwidthGbs:0.00} GB/s\n" +
        $"Disco: R {result.DiskReadMbs:0} MB/s W {result.DiskWriteMbs:0} MB/s\n" +
        $"Duración: {result.Elapsed.TotalSeconds:0.0}s";
}
