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
    [ObservableProperty] private string _contextNote = string.Empty;

    public async Task RunAsync(bool isBaseline, CancellationToken ct)
    {
        IsRunning = true;
        Status = "Midiendo…";
        CurrentStep = isBaseline ? "Paso 1: Creando línea base (3 trials + warmup)..." : "Paso 3: Midiendo después del cambio (3 trials + warmup)...";
        try
        {
            Directory.CreateDirectory(CaOPaths.BenchmarksDirectory);
            var runner = new SystemBenchmarkRunner();
            // Mediana de 3 trials con warmup: un pico aislado no fabrica mejoras (§46-48).
            var result = await runner.RunTrialsAsync(trials: 3, warmup: true, workloadId: isBaseline ? "baseline" : "after-change", ct);

            BaselineSummary = Describe(result);
            ContextNote = DescribeContext(result);

            if (isBaseline)
            {
                await File.WriteAllTextAsync(BaselinePath, JsonSerializer.Serialize(result), ct);
                ComparisonSummary = "✓ Línea base guardada (Paso 1 completado, mediana de 3 trials). Ahora aplique UN cambio en Optimizar (p. ej. plan Alto rendimiento) y vuelva para Paso 3 con las mismas condiciones (misma batería/AC, sin otras apps pesadas).";
                Verdict = "Línea base lista";
                CurrentStep = "Paso 2: Aplique UNA optimización en Optimizar";
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
                if (!string.Equals(baseline.Header.PowerState, result.Header.PowerState, StringComparison.OrdinalIgnoreCase))
                {
                    ContextNote += $"\n⚠ Condiciones distintas: base en '{baseline.Header.PowerState}', ahora en '{result.Header.PowerState}'. Para comparar, repita con la misma alimentación (idealmente AC).";
                }
                var comparison = SystemBenchmarkRunner.CompareFull(baseline, result);
                // Veredicto honesto con suelo de ruido §33 (CPU/memoria deciden; disco es informativo)
                var noise = SystemBenchmarkRunner.NoiseFloorPercent;
                ComparisonSummary =
                    $"CPU: {comparison.CpuDeltaPercent:+0.0;-0.0}% | Memoria: {comparison.MemoryDeltaPercent:+0.0;-0.0}% | Disco R: {comparison.DiskReadDeltaPercent:+0.0;-0.0}% W: {comparison.DiskWriteDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs} " +
                    $"(suelo ±{noise:0}%, mediana 3 trials).\n" +
                    $"Paso 5 — Veredicto: {comparison.VerdictEs}\n" +
                    (comparison.VerdictEs == "Regresión" ? "Regresión — Recomendación: revertir el cambio en Restaurar y repetir la medición." : comparison.VerdictEs == "Sin mejora medible" ? $"Sin mejora medible (dentro de ±{noise}%) — sin evidencia para mantener el cambio." : "Mejora medida — puede mantenerse si es estable tras reiniciar y repetir.");
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
        $"{result.WorkloadId} @ {result.Header.TimestampUtc.ToLocalTime():g} · {result.Header.PowerState.ToUpperInvariant()} · mediana 3 trials\n" +
        $"CPU: {result.CpuScore:0} | Memoria: {result.MemoryBandwidthGbs:0.00} GB/s\n" +
        $"Disco: R {result.DiskReadMbs:0} MB/s W {result.DiskWriteMbs:0} MB/s\n" +
        $"Duración: {result.Elapsed.TotalSeconds:0.0}s";

    private static string DescribeContext(SystemBenchmarkResult result) =>
        result.Header.PowerState == "battery"
            ? "🔋 Midiendo con batería: el sistema limita CPU/GPU. Para comparar, conecte el cargador (AC) y cierre apps pesadas."
            : "🔌 Midiendo con AC: correcto para comparar. Cierre juegos/navegador pesado y repita en igualdad de condiciones.";
}
