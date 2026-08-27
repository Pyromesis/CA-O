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
    [ObservableProperty] private string _currentStep = "Paso 1: Crear línea base";
    [ObservableProperty] private string _verdict = string.Empty;

    public async Task RunAsync(bool isBaseline, CancellationToken ct)
    {
        IsRunning = true;
        Status = "Midiendo…";
        CurrentStep = isBaseline ? "Paso 1: Creando línea base..." : "Paso 3: Midiendo después del cambio...";
        try
        {
            Directory.CreateDirectory(CaOPaths.BenchmarksDirectory);
            var runner = new SystemBenchmarkRunner();
            var result = await runner.RunAsync(isBaseline ? "baseline" : "after-change", ct);

            BaselineSummary = Describe(result);

            if (isBaseline)
            {
                await File.WriteAllTextAsync(BaselinePath, JsonSerializer.Serialize(result), ct);
                ComparisonSummary = "✓ Línea base guardada (Paso 1 completado). Ahora aplique una optimización y vuelva para Paso 3.";
                Verdict = "Línea base lista";
                CurrentStep = "Paso 2: Aplique una optimización en Optimizar";
                Status = "Línea base completada";
            }
            else
            {
                CurrentStep = "Paso 4: Comparando...";
                if (!File.Exists(BaselinePath))
                {
                    ComparisonSummary = "No hay línea base guardada; mida primero la línea base (Paso 1).";
                    Verdict = "Sin datos";
                    return;
                }
                var baseline = JsonSerializer.Deserialize<SystemBenchmarkResult>(await File.ReadAllTextAsync(BaselinePath, ct));
                if (baseline is null) { ComparisonSummary = "La línea base guardada no es legible."; Verdict = "Error"; return; }
                var comparison = SystemBenchmarkRunner.Compare(baseline, result);
                // Veredicto honesto con suelo de ruido §33
                var noise = SystemBenchmarkRunner.NoiseFloorPercent;
                ComparisonSummary =
                    $"CPU: {comparison.CpuDeltaPercent:+0.0;-0.0}% | Memoria: {comparison.MemoryDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs} " +
                    $"(suelo ±{noise:0}%).\n" +
                    $"Paso 5 — Veredicto: {comparison.VerdictEs}\n" +
                    (comparison.VerdictEs == "Regresión" ? "Regresión — Recomendación: revertir el cambio." : comparison.VerdictEs == "Sin mejora medible" ? $"Sin mejora medible (dentro de ±{noise}%) — sin evidencia para mantener." : "Mejora medida — puede mantenerse si es estable.");
                Verdict = comparison.VerdictEs;
                CurrentStep = $"Paso 5: {comparison.VerdictEs}";
                Status = $"Benchmark completado — {comparison.VerdictEs}";
            }
        }
        catch (OperationCanceledException) { Status = "Benchmark cancelado."; Verdict = "Cancelado"; }
        catch (Exception ex)
        {
            ComparisonSummary = $"{ErrorCodes.UiBenchmarkFailed}: El benchmark no pudo completarse. [Técnico: {ex.GetType().Name}]";
            Status = $"{ErrorCodes.UiBenchmarkFailed}: benchmark fallido";
            Verdict = "Error";
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
