namespace CAO.Core.Abstractions;

/// <summary>Persists optimization snapshots so reverts work across sessions.</summary>
public interface ISnapshotStore
{
    void Save(string optimizationId, OptimizationSnapshot snapshot);

    bool TryLoad(string optimizationId, out OptimizationSnapshot snapshot);

    void Delete(string optimizationId);
}
