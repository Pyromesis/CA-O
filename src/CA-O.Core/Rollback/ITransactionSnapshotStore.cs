using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Rollback;

/// <summary>
/// Immutable, transaction-scoped snapshot (FASE 7 v3 / P0-3). Identity is
/// the TransactionId — NEVER the optimization id. Two runs of the same
/// optimization coexist as independent directories.
/// </summary>
public sealed record TransactionSnapshotRecord
{
    public required TransactionSnapshotManifest Manifest { get; init; }

    public required OptimizationSnapshot State { get; init; }
}

/// <summary>Manifest written beside the state, including integrity hash.</summary>
public sealed record TransactionSnapshotManifest
{
    public required Guid TransactionId { get; init; }

    public required string OptimizationId { get; init; }

    /// <summary>Hash of the optimization definition id+category at capture time.</summary>
    public required string DefinitionVersion { get; init; }

    /// <summary>Snapshot schema version (current = 3).</summary>
    public required int SchemaVersion { get; init; }

    public required string AppVersion { get; init; }

    public required int WindowsBuild { get; init; }

    public required DateTime TimestampUtc { get; init; }

    /// <summary>SHA-256 over the canonical serialization of snapshot.json (computed by the store).</summary>
    public string? StateSha256 { get; set; }

    /// <summary>Caller SID when the operation came through the privileged pipe.</summary>
    public string? RequestedBySid { get; init; }
}

/// <summary>Primary snapshot identity is TRANSACTIONAL (P0-3).</summary>
public interface ISnapshotStore
{
    /// <summary>Atomically persists snapshot.json + manifest.json + integrity.json under snapshots/{txid}/.</summary>
    void Save(TransactionSnapshotRecord record);

    bool TryLoad(Guid transactionId, out TransactionSnapshotRecord? record);

    /// <summary>Newest snapshot captured for an optimization (revert-by-id path).</summary>
    bool TryLoadLatestForOptimization(string optimizationId, out TransactionSnapshotRecord? record);

    void Delete(Guid transactionId);

    IReadOnlyList<TransactionSnapshotRecord> ListAll();

    // ---- legacy v2 (flat files keyed by OptimizationId): read/delete only ----
    bool TryLoadLegacy(string optimizationId, out OptimizationSnapshot? snapshot);

    void DeleteLegacy(string optimizationId);

    IReadOnlyList<string> ListLegacyIds();
}

public static class TransactionSnapshotDefaults
{
    public const int SchemaVersion = 3;
}
