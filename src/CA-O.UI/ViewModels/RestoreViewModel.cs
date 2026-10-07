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

    /// <summary>
    /// Operaciones pendientes de recuperacion. CAO-BUG-2026-10-06 (N-1): antes solo
    /// se mostraba el texto y no habia ninguna accion posible, con el servicio
    /// rechazando tanto aplicar como revertir (<c>CAO-TXN-004</c>).
    /// </summary>
    [ObservableProperty] private IReadOnlyList<RecoveryCandidateInfo> _pendingRecoveries = Array.Empty<RecoveryCandidateInfo>();
    [ObservableProperty] private bool _hasPendingRecoveries;

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
        catch (Exception ex)
        {
            // Degradado honesto: NO vaciar la lista previa ni fingir "sin
            // snapshots" — se conserva lo último conocido y se informa el
            // motivo en RecoveryHint para que el usuario reintente.
            RecoveryHint = $"No se pudo leer snapshots ({ex.GetType().Name}): {ex.Message}";
        }
        // No se pisa un error de lectura con el mensaje por defecto.
        if (string.IsNullOrEmpty(RecoveryHint))
            RecoveryHint = _state.RecoveryCandidates.Count == 0
                ? "Sin recuperaciones pendientes."
                : $"Recuperación requerida: {string.Join(", ", _state.RecoveryCandidates.Select(r => r.OptimizationId))}";
        PendingRecoveries = _state.RecoveryCandidates;
        HasPendingRecoveries = _state.RecoveryCandidates.Count > 0;
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

    /// <summary>
    /// Cierra una transaccion pendiente de recuperacion. CAO-BUG-2026-10-06 (N-1):
    /// mientras el servicio detecta una recuperacion pendiente rechaza tanto
    /// <c>ApplyAsync</c> como <c>RevertAsync</c> con <c>CAO-TXN-004</c>, y
    /// <c>CrashRecoveryService.MarkRecovered</c> no lo llamaba nadie, de modo que el
    /// equipo quedaba sin salida. Con <paramref name="discardChanges"/> en falso se
    /// revierte desde el snapshot; en true el usuario acepta el estado actual y solo
    /// se cierra la entrada del journal.
    /// </summary>
    /// </summary>
    /// No es un <c>[RelayCommand]</c>: CommunityToolkit no genera un comando para
    /// una firma <c>(Guid, bool, CancellationToken)</c> y no merece la pena
    /// empaquetar los dos argumentos en un tipo solo por eso.
    /// </summary>
    public async Task RecoverAsync(Guid transactionId, bool discardChanges, CancellationToken ct = default)
    {
        try
        {
            var resp = await _pipe.RecoverAsync(transactionId, discardChanges, ct);
            RecoveryHint = resp is { Accepted: true }
                ? (discardChanges
                    ? "✓ Operación descartada: los cambios NO se revirtieron y la transacción quedó cerrada."
                    : "✓ Operación recuperada: los cambios se revirtieron y la transacción quedó cerrada.")
                : $"Rechazado [{resp?.ErrorCode}]: {resp?.SafeMessage}";
        }
        catch (Exception ex)
        {
            RecoveryHint = $"Recuperación falló (servicio no disponible): {ex.Message}";
        }
    }
}
