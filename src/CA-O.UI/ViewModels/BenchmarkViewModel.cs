using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Core.Engine;
using CAO.Infrastructure.Benchmarking;
using CAO.Infrastructure.Logging;
using CAO.Infrastructure.Networking;
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
    [ObservableProperty] private string _dnsSummary = string.Empty;
    [ObservableProperty] private string _fluencySummary = string.Empty;
    [ObservableProperty] private string _bootSummary = string.Empty;

    private IReadOnlyList<DnsBenchmarkResult>? _dnsLastResults;

    /// <summary>Últimos resultados DNS medidos (para pintar barras en la UI).</summary>
    public IReadOnlyList<DnsBenchmarkResult>? DnsLastResults
    {
        get => _dnsLastResults;
        set => SetProperty(ref _dnsLastResults, value);
    }

    private string? _lastSessionPath;

    /// <summary>Ruta de la última sesión guardada (la UI la recarga para tabla+sparkline).</summary>
    public string? LastSessionPath
    {
        get => _lastSessionPath;
        set => SetProperty(ref _lastSessionPath, value);
    }

    public async Task RunAsync(bool isBaseline, CancellationToken ct) =>
        await RunForOptimizationAsync("manual", "", isBaseline, ct);

    public async Task RunForOptimizationAsync(string optimizationId, string category, bool isBaseline, CancellationToken ct)
    {
        IsRunning = true;
        Status = "Midiendo…";
        CurrentStep = isBaseline ? "Paso 1: Midiendo línea base…" : "Paso 3: Midiendo tras el cambio…";
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
                LastSessionPath = BenchmarkStore.SessionPathFor(optimizationId, DateTime.UtcNow);
                await BenchmarkStore.SaveJsonAsync(LastSessionPath,
                    new BenchmarkSession(result, null, null, null, category, optimizationId), ct);
                ComparisonSummary = "✓ Línea base guardada (mediana de 3 trials).";
                Verdict = "Línea base lista";
                CurrentStep = "Paso 1: Línea base guardada";
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
                CurrentStep = "Paso 4: Comparando resultados…";
                var comparison = SystemBenchmarkRunner.CompareFull(baseline, result, parsed);
                ComparisonSummary = $"CPU: {comparison.CpuDeltaPercent:+0.0;-0.0}% | Memoria: {comparison.MemoryDeltaPercent:+0.0;-0.0}% | Disco R: {comparison.DiskReadDeltaPercent:+0.0;-0.0}% W: {comparison.DiskWriteDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs} (suelo ±{SystemBenchmarkRunner.NoiseFloorPercent:0}%, mediana 3 trials).\n{comparison.ReasonEs}";
                Verdict = comparison.VerdictEs;
                CurrentStep = $"Paso 5: Veredicto — {comparison.VerdictEs}";
                LastSessionPath = BenchmarkStore.SessionPathFor(optimizationId, DateTime.UtcNow);
                await BenchmarkStore.SaveJsonAsync(LastSessionPath,
                    new BenchmarkSession(baseline, result, null, null, category, optimizationId), ct);
                try
                {
                    new JsonHistoryLogger().Log(new HistoryEntry
                    {
                        TimestampUtc = DateTime.UtcNow,
                        AppVersion = AppVersion.Semantic,
                        OptimizationId = optimizationId,
                        Operation = "benchmark",
                        Success = comparison.VerdictEs != "Regresión",
                        BenchmarkSummary = $"CPU {comparison.CpuDeltaPercent:+0.0;-0.0}% MEM {comparison.MemoryDeltaPercent:+0.0;-0.0}% R {comparison.DiskReadDeltaPercent:+0.0;-0.0}% W {comparison.DiskWriteDeltaPercent:+0.0;-0.0}% — {comparison.VerdictEs}",
                    });
                }
                catch { /* el historial nunca rompe el benchmark */ }
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

    public async Task MeasureDnsAsync(CancellationToken ct)
    {
        try
        {
            var provider = new DnsBenchmarkProvider();
            var results = await provider.BenchmarkAsync(null, ct);
            var best = DnsBenchmarkProvider.PickBest(results);
            DnsSummary = best is null ? "Sin respuesta DNS medible." :
                $"Mejor: {best.Resolver} {best.MedianLatencyMs:0.0} ms (jitter {best.JitterMs:0.0} ms, {best.Successes}/{best.Attempts})";
            DnsLastResults = results; // propiedad para pintar barras + guardar DnsAfterMs si hay sesión
        }
        catch (OperationCanceledException) { DnsSummary = "Medición DNS cancelada."; }
        catch (Exception ex) { DnsSummary = $"{ErrorCodes.UiBenchmarkFailed}: DNS no medido. [Técnico: {ex.GetType().Name}]"; }
    }

    public async Task MeasureFluencyAsync(IFrameCapture capture, CancellationToken ct)
    {
        try
        {
            var result = await capture.CaptureAsync(TimeSpan.FromSeconds(10), ct);
            if (result is null) { FluencySummary = CAO.UI.Localizer.Get("benchmark.unavailable"); return; }
            var stats = BenchmarkAnalyzer.AnalyzeFrameTimes(result.FrameTimesMs);
            FluencySummary = $"avg {stats.AverageFps:0} FPS · 1% low {stats.OnePercentLowFps:0} · P99 {stats.P99FrameTimeMs:0.0} ms · DPC {result.DpcPercent:0.0}%";
        }
        catch (OperationCanceledException) { FluencySummary = "Medición cancelada."; }
        catch (Exception ex) { FluencySummary = $"{ErrorCodes.UiBenchmarkFailed}: fluidez no medida. [Técnico: {ex.GetType().Name}]"; }
    }

    public string ExportSessionCsv(BenchmarkSession session)
    {
        var sb = new StringBuilder("metrica;antes;despues;delta_%\n");
        void Row(string name, double before, double after) =>
            sb.AppendLine($"{name};{before:0.00};{after:0.00};{SystemBenchmarkRunner.PercentChange(before, after):+0.00;-0.00}");
        Row("cpu_ops", session.Before.CpuScore, session.After?.CpuScore ?? 0);
        Row("mem_gbs", session.Before.MemoryBandwidthGbs, session.After?.MemoryBandwidthGbs ?? 0);
        Row("disk_r_mbs", session.Before.DiskReadMbs, session.After?.DiskReadMbs ?? 0);
        Row("disk_w_mbs", session.Before.DiskWriteMbs, session.After?.DiskWriteMbs ?? 0);
        return sb.ToString();
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
