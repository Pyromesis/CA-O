using CAO.Core.Abstractions;
using CAO.Core.Rollback;

namespace CAO.Infrastructure.Persistence;

/// <summary>
/// Repository fachada §17: UI nunca llama Directory.GetDirectories directamente.
/// Expone snapshots con validación e información real para Restore.
/// </summary>
public sealed class SnapshotRepository
{
    private readonly FileSnapshotStore _store;

    public SnapshotRepository(FileSnapshotStore store) => _store = store;

    public Task<IReadOnlyList<TransactionSnapshotRecord>> GetAllSnapshotsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TransactionSnapshotRecord>>(_store.ListAll().OrderByDescending(s => s.Manifest.TimestampUtc).ToList());

    public Task<TransactionSnapshotRecord?> GetSnapshotAsync(Guid transactionId, CancellationToken ct = default)
    {
        _store.TryLoad(transactionId, out var rec);
        return Task.FromResult(rec);
    }

    public Task<TransactionSnapshotRecord?> GetLatestSnapshotForOptimizationAsync(string optimizationId, CancellationToken ct = default)
    {
        _store.TryLoadLatestForOptimization(optimizationId, out var rec);
        return Task.FromResult(rec);
    }

    public Task<bool> DeleteSnapshotAsync(Guid transactionId, CancellationToken ct = default)
    {
        try { _store.Delete(transactionId); return Task.FromResult(true); }
        catch { return Task.FromResult(false); }
    }

    public Task<bool> ValidateSnapshotAsync(Guid transactionId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryLoad(transactionId, out var rec) && rec != null);

    public sealed record SnapshotInfo(
        Guid TransactionId,
        string OptimizationId,
        DateTime TimestampUtc,
        int EntryCount,
        int WindowsBuild,
        string AppVersion,
        string? RequestedBySid,
        bool IsValid
    );

    public async Task<IReadOnlyList<SnapshotInfo>> GetSnapshotInfosAsync(CancellationToken ct = default)
    {
        var all = await GetAllSnapshotsAsync(ct);
        return all.Select(r => new SnapshotInfo(
            r.Manifest.TransactionId,
            r.Manifest.OptimizationId,
            r.Manifest.TimestampUtc,
            r.State.Registry.Count,
            r.Manifest.WindowsBuild,
            r.Manifest.AppVersion,
            r.Manifest.RequestedBySid,
            true
        )).ToList();
    }
}
