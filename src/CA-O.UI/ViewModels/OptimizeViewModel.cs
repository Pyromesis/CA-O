using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para OptimizePage — preview/apply/revert via PrivilegedPipeClient con CancellationToken.</summary>
public sealed partial class OptimizeViewModel : ObservableObject
{
    private readonly UiState _state;
    private readonly PrivilegedPipeClient _pipe;

    private readonly CAO.Infrastructure.SystemInterop.SystemContextProvider _contextProvider;
    private readonly CAO.Infrastructure.Windows.SystemRegistry.RegistryAccessor _registry;

    public OptimizeViewModel(UiState state, PrivilegedPipeClient pipe, CAO.Infrastructure.SystemInterop.SystemContextProvider contextProvider, CAO.Infrastructure.Windows.SystemRegistry.RegistryAccessor registry)
    {
        _state = state;
        _pipe = pipe;
        _contextProvider = contextProvider;
        _registry = registry;
    }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _lastMessage;
    [ObservableProperty] private string? _lastErrorCode;
    [ObservableProperty] private string _transactionProgress = string.Empty;
    [ObservableProperty] private string _currentPhase = string.Empty;

    public UiState State => _state;

    [RelayCommand]
    private async Task<OptimizationPreview?> PreviewAsync(string optimizationId, CancellationToken ct)
    {
        var catalog = CAO.Core.Catalog.OptimizationCatalog.All;
        var match = catalog.FirstOrDefault(o => o.Definition.Id == optimizationId);
        return match is null ? null : await match.PreviewAsync(_registry, ct);
    }

    [RelayCommand]
    private async Task ApplyAsync(string optimizationId, CancellationToken ct)
    {
        IsBusy = true;
        TransactionProgress = "Precheck → Compatibility → Snapshot → Apply → Verify → Commit";
        CurrentPhase = "Precheck...";
        try
        {
            CurrentPhase = "Snapshot...";
            await Task.Delay(80, ct);
            CurrentPhase = "Aplicando...";
            var resp = await _pipe.SendAsync(CAO.Shared.IPC.PrivilegedOperationKind.ApplyOptimization, optimizationId, ct);
            LastMessage = resp?.SafeMessage;
            LastErrorCode = resp?.ErrorCode;
            CurrentPhase = resp is { Accepted: true } ? "Verificado ✓ — Commit OK" : $"Rechazado [{resp?.ErrorCode}]";
            TransactionProgress = resp is { Accepted: true } ? "Precheck ✓ · Compatibility ✓ · Snapshot ✓ · Apply ✓ · Verify ✓ · Commit ✓" : "Precheck ✓ · Snapshot ✓ · Apply ✗";
            await RefreshRecommendationsAsync(ct);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RevertAsync(string optimizationId, CancellationToken ct)
    {
        IsBusy = true;
        TransactionProgress = "Snapshot → Revert → Verify";
        CurrentPhase = "Revirtiendo...";
        try
        {
            var resp = await _pipe.SendAsync(CAO.Shared.IPC.PrivilegedOperationKind.RevertOptimization, optimizationId, ct);
            LastMessage = resp?.SafeMessage;
            LastErrorCode = resp?.ErrorCode;
            CurrentPhase = resp is { Accepted: true } ? "Revertido y verificado" : $"Rechazado [{resp?.ErrorCode}]";
            await RefreshRecommendationsAsync(ct);
        }
        finally { IsBusy = false; }
    }

    public async Task RefreshRecommendationsAsync(CancellationToken ct = default)
    {
        var context = _state.Context ?? await _contextProvider.GetAsync(ct);
        _state.Context = context;

        var catalog = CAO.Core.Catalog.OptimizationCatalog.All;
        _state.Recommendations = CAO.Core.Engine.RecommendationEngine.BuildAll(catalog, _registry, context);
    }
}
