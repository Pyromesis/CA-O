using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para DashboardPage — expone Health y navegación sin code-behind de negocio.</summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly UiState _state;
    private readonly CAO.Core.Abstractions.IAnalysisCoordinator _analysisService;
    private readonly Infrastructure.Persistence.AnalysisStateStore _store;
    private readonly Infrastructure.Logging.StructuredLogger _logger;

    private readonly CAO.Infrastructure.SystemInterop.SystemContextProvider _contextProvider;
    private readonly CAO.Core.Rollback.CrashRecoveryService _recoveryService;

    public DashboardViewModel(UiState state, CAO.Core.Abstractions.IAnalysisCoordinator analysisService, Infrastructure.Persistence.AnalysisStateStore store, Infrastructure.Logging.StructuredLogger logger, CAO.Infrastructure.SystemInterop.SystemContextProvider contextProvider, CAO.Core.Rollback.CrashRecoveryService recoveryService)
    {
        _state = state;
        _analysisService = analysisService;
        _store = store;
        _logger = logger;
        _contextProvider = contextProvider;
        _recoveryService = recoveryService;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _serviceStatus = "unknown";
    [ObservableProperty] private SystemDiagnosticReport? _health;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private IReadOnlyList<Recommendation> _recommendations = Array.Empty<Recommendation>();

    public UiState State => _state;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            var correlation = CAO.Shared.Correlation.New();
            try { _logger.Info("Dashboard", $"LoadAsync correlation={correlation}", correlation); } catch { }
            // Cargar análisis persistido si existe (§4)
            var persisted = _store.LoadLatestAnalysis();
            if (persisted?.Context != null)
            {
                _state.Context = persisted.Context;
                _state.Recommendations = persisted.Recommendations ?? Array.Empty<Recommendation>();
                _state.LastAnalysisUtc = persisted.TimestampUtc;
                Health = persisted.Health ?? Core.Diagnostics.HealthEngine.Evaluate(persisted.Context);
                Recommendations = _state.Recommendations;
                StatusMessage = _store.GetStatusLabel(persisted);
            }
            else
            {
                var context = _state.Context ?? await _contextProvider.GetAsync(ct);
                _state.Context = context;
                Health = Core.Diagnostics.HealthEngine.Evaluate(context);
                Recommendations = _state.Recommendations;
            }
            ServiceStatus = _state.ServiceStatus;
        }
        catch (OperationCanceledException) { StatusMessage = "Carga cancelada."; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AnalyzeAsync(CancellationToken ct)
    {
        IsLoading = true;
        StatusMessage = "Analizando…";
        try
        {
            var correlation = CAO.Shared.Correlation.New();
            try { _logger.Info("Dashboard", $"Analyze correlation={correlation}", correlation); } catch { }
            var candidates = _recoveryService.Scan();
            _state.RecoveryCandidates = candidates.Select(c => c.OptimizationId).ToList();

            var result = await _analysisService.RunAsync(ct);
            if (result.Context != null) _state.Context = result.Context;
            _state.Recommendations = result.Recommendations;
            Recommendations = result.Recommendations;
            Health = result.Health;
            _state.LastAnalysisUtc = DateTime.UtcNow;
            StatusMessage = result.AnalysisState == "Completed" ? "Análisis completo."
                : result.AnalysisState == "CompletedWithWarnings" ? $"Completado con advertencias ({result.Warnings.Count})"
                : result.AnalysisState == "Cancelled" ? "Análisis cancelado."
                : "Análisis fallido";
        }
        catch (OperationCanceledException) { StatusMessage = "Análisis cancelado."; }
        catch (Exception ex)
        {
            StatusMessage = $"{ErrorCodes.UiAnalyzeFailed}: análisis no completado";
            App.WriteCrashLog(ex);
        }
        finally { IsLoading = false; }
    }
}
