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
    private readonly CAO.Core.Abstractions.IAnalysisCoordinator _analysisService;
    private readonly UiState _uiState;
    private readonly Infrastructure.Logging.StructuredLogger _logger;
    private CancellationTokenSource? _cts;

    public AnalyzeViewModel(CAO.Core.Abstractions.IAnalysisCoordinator analysisService, UiState uiState, Infrastructure.Logging.StructuredLogger logger)
    {
        _analysisService = analysisService;
        _uiState = uiState;
        _logger = logger;
    }

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
    /// Delega al SystemAnalysisService unificado (§5) — elimina duplicación Dashboard/Analyze.
    /// Mantiene estado por módulo para UI pero la verdad proviene del servicio.
    /// </summary>
    public void RestoreFromState(CAO.Shared.SystemContext? context, IReadOnlyList<AnalysisModuleResult>? modules = null)
    {
        if (context == null) return;
        // Hydrate module results from persisted state without calling ResetResults
        OverallStatus = $"Datos del {_uiState.LastAnalysisUtc?.ToLocalTime():g} cargados.";
        if (modules != null)
        {
            foreach (var r in modules)
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
        }
    }

    public void HydrateFromPersistedAnalysis(CAO.Infrastructure.Persistence.AnalysisStateStore.PersistedAnalysis? persisted)
    {
        if (persisted?.Context == null) return;
        OverallStatus = persisted.AnalysisState == "CompletedWithWarnings" ? "Análisis completado con advertencias" : "Análisis completo";
        // Populate module placeholders as Completed from persisted health
        NetworkResult = new AnalysisModuleResult("Network", AnalysisModuleStatus.Completed, persisted.Duration, "persistido", null, null, Array.Empty<string>());
        SecurityResult = new AnalysisModuleResult("Security", AnalysisModuleStatus.Completed, TimeSpan.Zero, "persistido", null, null, Array.Empty<string>());
        StorageResult = new AnalysisModuleResult("Storage", AnalysisModuleStatus.Completed, TimeSpan.Zero, "persistido", null, null, Array.Empty<string>());
        DriversResult = new AnalysisModuleResult("Drivers", AnalysisModuleStatus.Completed, TimeSpan.Zero, "persistido", null, null, Array.Empty<string>());
        SystemResult = new AnalysisModuleResult("System", AnalysisModuleStatus.Completed, TimeSpan.Zero, $"{persisted.Context.CpuName}", null, null, Array.Empty<string>());
        ThermalResult = new AnalysisModuleResult("Thermal", AnalysisModuleStatus.Completed, TimeSpan.Zero, persisted.Context.ThermalState.ToString(), null, null, Array.Empty<string>());
    }

    public async Task<IReadOnlyList<AnalysisModuleResult>> RunAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Array.Empty<AnalysisModuleResult>();
        var correlationId = CAO.Shared.Correlation.New();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsRunning = true;
        OverallStatus = "Midiendo…";
        ResetResults();
        try { _logger.Info("Analyze", $"Run started correlation={correlationId}", correlationId); } catch { }

        try
        {
            var result = await _analysisService.RunAsync(_cts.Token);
            // Mapear ModuleResult del servicio a AnalysisModuleResult de la UI
            var mapped = result.Modules.Select(m => new AnalysisModuleResult(
                m.Module,
                m.Success ? AnalysisModuleStatus.Completed : m.ErrorCode == "CAO-CANCELLED" ? AnalysisModuleStatus.Cancelled : AnalysisModuleStatus.Failed,
                m.Duration,
                m.Value,
                m.ErrorCode,
                m.Warning,
                Array.Empty<string>())).ToList();

            foreach (var r in mapped)
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

            // Actualizar UiState global desde el servicio (§4 persistencia ya hecha en el servicio)
            if (result.Context != null) _uiState.Context = result.Context;
            if (result.Recommendations.Count > 0) _uiState.Recommendations = result.Recommendations;
            _uiState.LastAnalysisUtc = DateTime.UtcNow;

            var failed = mapped.Count(r => r.Status == AnalysisModuleStatus.Failed);
            var cancelled = mapped.Count(r => r.Status == AnalysisModuleStatus.Cancelled);
            OverallStatus = result.AnalysisState == "Cancelled" ? $"Cancelado ({mapped.Count - cancelled}/{mapped.Count} completados)"
                : failed == 0 ? "Análisis completo — ningún cambio aplicado."
                : failed < mapped.Count ? $"Análisis completado con advertencias ({failed} módulos con fallo)"
                : "Análisis fallido";

            try { _logger.Info("Analyze", $"Run finished {OverallStatus} correlation={result.CorrelationId}", result.CorrelationId); } catch { }
            return mapped;
        }
        catch (OperationCanceledException)
        {
            OverallStatus = "Análisis cancelado.";
            return Array.Empty<AnalysisModuleResult>();
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            CancelCommand.NotifyCanExecuteChanged();
        }
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
