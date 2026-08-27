using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para RestorePage — snapshots y recuperación.</summary>
public sealed partial class RestoreViewModel : ObservableObject
{
    private readonly UiState _state;
    private readonly PrivilegedPipeClient _pipe;

    public RestoreViewModel(UiState state, PrivilegedPipeClient pipe)
    {
        _state = state;
        _pipe = pipe;
    }

    [ObservableProperty] private IReadOnlyList<string> _snapshots = Array.Empty<string>();
    [ObservableProperty] private string _recoveryHint = string.Empty;
    [ObservableProperty] private bool _isEmpty;

    [RelayCommand]
    private void Refresh()
    {
        var directory = CaOPaths.SnapshotsDirectory;
        if (!Directory.Exists(directory))
        {
            Snapshots = Array.Empty<string>();
            IsEmpty = true;
            RecoveryHint = _state.RecoveryCandidates.Count == 0 ? "Sin recuperaciones pendientes." : $"Recuperación requerida: {string.Join(", ", _state.RecoveryCandidates)}";
            return;
        }
        var folders = Directory.GetDirectories(directory).Select(Path.GetFileName).Where(s => s is not null).Cast<string>().OrderByDescending(s => s).ToList();
        var files = Directory.GetFiles(directory, "*.json").Select(Path.GetFileNameWithoutExtension).Where(s => s is not null).Cast<string>().OrderBy(s => s).ToList();
        var combined = folders.Count > 0 ? folders! : files;
        Snapshots = combined;
        IsEmpty = combined.Count == 0;
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
