using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Infrastructure.Networking;
using CAO.Infrastructure.Security;
using CAO.Infrastructure.Storage;
using CAO.Infrastructure.SystemInterop;

namespace CAO.UI.ViewModels;

/// <summary>
/// MVVM ViewModel para AnalyzePage ( §5, §6, §8-10 ).
/// Cada módulo tiene estado individual (Pending/Running/Completed/Failed/Cancelled) y
/// se ejecutan en paralelo con Task.WhenAll respetando CancellationToken y capturando
/// errores por módulo sin abortar el análisis global.
/// </summary>
public enum AnalysisModuleStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    Cancelled,
}

public sealed record AnalysisModuleResult(
    string Module,
    AnalysisModuleStatus Status,
    TimeSpan Duration,
    string? Value,
    string? ErrorCode,
    string? Message,
    IReadOnlyList<string> Warnings);

public sealed partial class AnalyzeViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _overallStatus = "Inactivo";
    [ObservableProperty] private AnalysisModuleResult? _networkResult;
    [ObservableProperty] private AnalysisModuleResult? _securityResult;
    [ObservableProperty] private AnalysisModuleResult? _storageResult;
    [ObservableProperty] private AnalysisModuleResult? _driversResult;
    [ObservableProperty] private AnalysisModuleResult? _systemResult;
    [ObservableProperty] private AnalysisModuleResult? _thermalResult;
    [ObservableProperty] private AnalysisModuleResult? _inputResult;

    public bool CanCancel => IsRunning;

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(CanCancel));

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        try { _cts?.Cancel(); } catch { }
    }

    /// <summary>
    /// Ejecuta todos los módulos independientes en paralelo ( §8 ).
    /// Respeta límites de recursos (máx 6 tareas concurrentes) y nunca deja tareas huérfanas.
    /// </summary>
    public async Task<IReadOnlyList<AnalysisModuleResult>> RunAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Array.Empty<AnalysisModuleResult>();
        var correlationId = CAO.Shared.Correlation.New(); // §159 conecta UI logs
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsRunning = true;
        OverallStatus = "Midiendo…";
        ResetResults();
        try { AppHost.Resolve<Infrastructure.Logging.StructuredLogger>().Info("Analyze", $"Run started correlation={correlationId}", correlationId); } catch { }

        var token = _cts.Token;
        var results = new List<AnalysisModuleResult>();
        var sw = Stopwatch.StartNew();

        try
        {
            // Módulos independientes → WhenAll con manejo individual de errores ( §10 )
            var tasks = new List<Task<AnalysisModuleResult>>
            {
                MeasureModuleAsync("Network", token, async t =>
                {
                    var provider = new NetworkDiagnosticsProvider();
                    var data = await provider.MeasureAsync(t);
                    var summary = $"{data.Interfaces.Count} interfaces, {data.Measurements.Count} mediciones";
                    return summary;
                }),
                MeasureModuleAsync("Security", token, t =>
                {
                    var data = new SecurityDiagnosticsProvider().Measure();
                    return Task.FromResult(string.Join(", ", data.Features.Select(f => $"{f.Name}:{(f.Enabled == true ? "on" : f.Enabled == false ? "off" : "?")}")));
                }),
                MeasureModuleAsync("Storage", token, t =>
                {
                    var data = new StorageDiagnosticsProvider().Measure();
                    return Task.FromResult($"{data.Volumes.Count} volúmenes");
                }),
                MeasureModuleAsync("Drivers", token, async t =>
                {
                    var data = await new DriverDiagnosticsProvider().MeasureAsync(t);
                    return $"{data.Drivers.Count} drivers";
                }),
                MeasureModuleAsync("System", token, async t =>
                {
                    var provider = new WmiSystemInfoProvider();
                    var data = await provider.GetAsync(t);
                    return $"{data.CpuName} / {data.RamGb}GB";
                }),
                MeasureModuleAsync("Thermal", token, async t =>
                {
                    var data = await new ThermalDiagnosticsProvider().MeasureAsync(t);
                    return $"{data.Zones.Count} zonas ({(data.IsAvailable ? "datos" : "sin datos")})";
                }),
            };

            var completed = await Task.WhenAll(tasks);
            results.AddRange(completed);

            // Asignar a props observables
            foreach (var r in completed)
            {
                switch (r.Module)
                {
                    case "Network": NetworkResult = r; break;
                    case "Security": SecurityResult = r; break;
                    case "Storage": StorageResult = r; break;
                    case "Drivers": DriversResult = r; break;
                    case "System": SystemResult = r; break;
                    case "Thermal": ThermalResult = r; break;
                }
            }

            var failed = completed.Count(r => r.Status == AnalysisModuleStatus.Failed);
            var cancelled = completed.Count(r => r.Status == AnalysisModuleStatus.Cancelled);
            OverallStatus = cancelled > 0 ? $"Cancelado ({completed.Length - cancelled}/{completed.Length} completados)"
                : failed == 0 ? "Análisis completo — ningún cambio aplicado."
                : failed < completed.Length ? $"Análisis completado con advertencias ({failed} módulos con fallo)"
                : "Análisis fallido";
        }
        catch (OperationCanceledException)
        {
            OverallStatus = "Análisis cancelado.";
        }
        finally
        {
            sw.Stop();
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            CancelCommand.NotifyCanExecuteChanged();
        }

        return results;
    }

    private static async Task<AnalysisModuleResult> MeasureModuleAsync(
        string module, CancellationToken ct, Func<CancellationToken, Task<string>> measure)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            var value = await measure(ct);
            sw.Stop();
            return new AnalysisModuleResult(module, AnalysisModuleStatus.Completed, sw.Elapsed, value, null, null, Array.Empty<string>());
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new AnalysisModuleResult(module, AnalysisModuleStatus.Cancelled, sw.Elapsed, null, "CAO-CANCELLED", "Cancelado por el usuario.", Array.Empty<string>());
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new AnalysisModuleResult(module, AnalysisModuleStatus.Failed, sw.Elapsed, null, "CAO-ANALYZE-001", ex.Message, Array.Empty<string>());
        }
    }

    private void ResetResults()
    {
        NetworkResult = new AnalysisModuleResult("Network", AnalysisModuleStatus.Running, TimeSpan.Zero, null, null, null, Array.Empty<string>());
        SecurityResult = new AnalysisModuleResult("Security", AnalysisModuleStatus.Running, TimeSpan.Zero, null, null, null, Array.Empty<string>());
        StorageResult = new AnalysisModuleResult("Storage", AnalysisModuleStatus.Running, TimeSpan.Zero, null, null, null, Array.Empty<string>());
        DriversResult = new AnalysisModuleResult("Drivers", AnalysisModuleStatus.Running, TimeSpan.Zero, null, null, null, Array.Empty<string>());
        SystemResult = new AnalysisModuleResult("System", AnalysisModuleStatus.Running, TimeSpan.Zero, null, null, null, Array.Empty<string>());
        ThermalResult = new AnalysisModuleResult("Thermal", AnalysisModuleStatus.Running, TimeSpan.Zero, null, null, null, Array.Empty<string>());
    }
}
