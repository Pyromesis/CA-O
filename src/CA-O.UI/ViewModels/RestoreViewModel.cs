using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para RestorePage — snapshots y recuperación.</summary>
public sealed partial class RestoreViewModel : ObservableObject
{
    private readonly UiState _state;
    private readonly PrivilegedPipeClient _pipe;
    private readonly Infrastructure.Persistence.SnapshotRepository _repository;

    public RestoreViewModel(UiState state, PrivilegedPipeClient pipe, Infrastructure.Persistence.SnapshotRepository repository)
    {
        _state = state;
        _pipe = pipe;
        _repository = repository;
    }

    [ObservableProperty] private IReadOnlyList<string> _snapshots = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<Infrastructure.Persistence.SnapshotRepository.SnapshotInfo> _snapshotInfos = Array.Empty<Infrastructure.Persistence.SnapshotRepository.SnapshotInfo>();
    [ObservableProperty] private string _recoveryHint = string.Empty;
    [ObservableProperty] private bool _isEmpty;

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var infos = await _repository.GetSnapshotInfosAsync(ct);
            SnapshotInfos = infos;
            Snapshots = infos.Select(i => $"{i.TimestampUtc:yyyy-MM-dd HH:mm} — {i.OptimizationId} — TX:{i.TransactionId.ToString()[..8]} — {i.EntryCount} valores — build {i.WindowsBuild}").ToList();
            IsEmpty = infos.Count == 0;
        }
        catch
        {
            // Degradado: si falla, mostrar vacío pero no crashear (§19)
            SnapshotInfos = Array.Empty<Infrastructure.Persistence.SnapshotRepository.SnapshotInfo>();
            Snapshots = Array.Empty<string>();
            IsEmpty = true;
        }
        RecoveryHint = _state.RecoveryCandidates.Count == 0 ? "Sin recuperaciones pendientes." : $"Recuperación requerida: {string.Join(", ", _state.RecoveryCandidates)}";
    }

    [RelayCommand]
    private async Task RevertAsync(string snapshotId, CancellationToken ct)
    {
        try
        {
            var resp = await _pipe.SendAsync(CAO.Shared.IPC.PrivilegedOperationKind.RevertOptimization, snapshotId, ct);
            RecoveryHint = resp is { Accepted: true } ? "✓ Reversión solicitada y aceptada." : $"Rechazado [{resp?.ErrorCode}]: {resp?.SafeMessage}";
        }
        catch (Exception ex)
        {
            RecoveryHint = $"Restauración falló (servicio no disponible): {ex.Message}";
        }
    }
}
