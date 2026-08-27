using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para DashboardPage — expone Health y navegación sin code-behind de negocio.</summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly UiState _state;

    public DashboardViewModel(UiState state) => _state = state;

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
            try { AppHost.Resolve<Infrastructure.Logging.StructuredLogger>().Info("Dashboard", $"LoadAsync correlation={correlation}", correlation); } catch { }
            var context = _state.Context ?? await AppServices.ContextProvider.GetAsync(ct);
            _state.Context = context;
            Health = Core.Diagnostics.HealthEngine.Evaluate(context);
            Recommendations = _state.Recommendations;
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
            try { AppHost.Resolve<Infrastructure.Logging.StructuredLogger>().Info("Dashboard", $"Analyze correlation={correlation}", correlation); } catch { }

            var candidates = AppServices.Recovery.Scan();
            _state.RecoveryCandidates = candidates.Select(c => c.OptimizationId).ToList();

            var context = await AppServices.ContextProvider.GetAsync(ct);
            _state.Context = context;

            // Diagnósticos en paralelo con cancellation (§8, §87 startup no bloqueante)
            var networkTask = new Infrastructure.Networking.NetworkDiagnosticsProvider().MeasureAsync(ct);
            var storageTask = Task.Run(() => new Infrastructure.Storage.StorageDiagnosticsProvider().Measure(), ct);
            var securityTask = Task.Run(() => new Infrastructure.Security.SecurityDiagnosticsProvider().Measure(), ct);

            await Task.WhenAll(networkTask, storageTask);

            var network = await networkTask;
            var storage = await storageTask;
            var security = await securityTask;

            var recommendations = await Task.Run(() => Core.Engine.RecommendationEngine.BuildAll(AppServices.Catalog, AppServices.Registry, context), ct);
            _state.Recommendations = recommendations;
            Recommendations = recommendations;

            var report = Core.Diagnostics.HealthEngine.Evaluate(context, network, storage, null, security);
            Health = report;
            _state.LastAnalysisUtc = DateTime.UtcNow;
            StatusMessage = "Análisis completo.";
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
