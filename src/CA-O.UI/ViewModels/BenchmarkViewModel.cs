using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para BenchmarkPage — benchmark A/B con trials y suelo de ruido explícito (§46-48).</summary>
public sealed partial class BenchmarkViewModel : ObservableObject
{
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _baselineSummary = string.Empty;
    [ObservableProperty] private string _comparisonSummary = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _currentStep = "Paso 1: Crear línea base";
    [ObservableProperty] private string _verdict = string.Empty;
    [ObservableProperty] private string _contextNote = string.Empty;

    public async Task RunAsync(bool isBaseline, CancellationToken ct) =>
        await RunForOptimizationAsync("manual", "", isBaseline, ct);

    public async Task RunForOptimizationAsync(string optimizationId, string category, bool isBaseline, CancellationToken ct)
    {
        IsRunning = true;
        Status = "Midiendo…";
        try
        {
            var runner = new SystemBenchmarkRunner();
            var result = await runner.RunTrialsAsync(trials: 3, warmup: true,
                workloadId: $"{optimizationId}-{(isBaseline ? "baseline" : "after")}", ct);
            BaselineSummary = Describe(result);
            ContextNote = DescribeContext(result);
            if (isBaseline)
            {
                await BenchmarkStore.SaveJsonAsync(BenchmarkStore.BaselinePath, result, ct);
                await BenchmarkStore.SaveJsonAsync(BenchmarkStore.SessionPathFor(optimizationId, DateTime.UtcNow),
                    new BenchmarkSession(result, null, null, null, category, optimizationId), ct);
                ComparisonSummary = "✓ Línea base guardada (mediana de 3 trials).";
                Verdict = "Línea base lista";
            }
            else
            {
                var baseline = await BenchmarkStore.LoadJsonAsync<SystemBenchmarkResult>(BenchmarkStore.BaselinePath, ct);
                if (baseline is null) { ComparisonSummary = "No hay línea base legible; mida primero la línea base."; Verdict = "Sin datos"; return; }
                if (!BenchmarkStore.IsBaselineValid(baseline, result, DateTime.UtcNow))
                {
                    ComparisonSummary = "La línea base caducó o cambiaron las condiciones (TTL 7 días, misma máquina/SO/versión/alimentación). Mida de nuevo la línea base.";
                    Verdict = "InsuficienteData";
                    return;
                }
                if (result.CpuCvPercent > 5 || result.MemoryCvPercent > 5)
                {
                    ComparisonSummary = $"Varianza alta entre trials (CV CPU {result.CpuCvPercent:0.0}% MEM {result.MemoryCvPercent:0.0}% > 5%): repita con el equipo en reposo.";
                    Verdict = "InsuficienteData";
                    return;
                }
                var parsed = Enum.TryParse<OptimizationCategory>(category, out var cat) ? cat : (OptimizationCategory?)null;
                var comparison = SystemBenchmarkRunner.CompareFull(baseline, result, parsed);
                ComparisonSummary = $"CPU: {comparison.CpuDeltaPercent:+0.0;-0.0}% | Memoria: {comparison.MemoryDeltaPercent:+0.0;-0.0}% | Disco R: {comparison.DiskReadDeltaPercent:+0.0;-0.0}% W: {comparison.DiskWriteDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs} (suelo ±3%, mediana 3 trials).\n{comparison.ReasonEs}";
                Verdict = comparison.VerdictEs;
                await BenchmarkStore.SaveJsonAsync(BenchmarkStore.SessionPathFor(optimizationId, DateTime.UtcNow),
                    new BenchmarkSession(baseline, result, null, null, category, optimizationId), ct);
            }
            Status = $"Benchmark completado — {Verdict}";
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
