using CAO.Shared;

namespace CAO.Core.Abstractions;

/// <summary>
/// Registry access abstraction so optimization logic can be unit-tested
/// against an in-memory store. Values are raw objects (DWORD/string).
/// </summary>
public interface IRegistryAccessor
{
    RegistryValueKind2 GetKind(RegistryHive2 hive, string keyPath, string valueName);

    object? GetValue(RegistryHive2 hive, string keyPath, string valueName);

    /// <summary>Writes a value creating intermediate keys as needed.</summary>
    void SetValue(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind);

    /// <summary>Deletes a value; returns false when it did not exist.</summary>
    bool DeleteValue(RegistryHive2 hive, string keyPath, string valueName);

    /// <summary>Returns all value names under a key (empty when the key is missing).</summary>
    IReadOnlyList<string> GetValueNames(RegistryHive2 hive, string keyPath);
}

/// <summary>Hive abstraction (maps to Microsoft.Win32.RegistryHive).</summary>
public enum RegistryHive2
{
    CurrentUser,
    LocalMachine,
}

/// <summary>Subset of RegistryValueKind used by the catalog.</summary>
public enum RegistryValueKind2
{
    DWord,
    QWord,
    String,
}

/// <summary>A single captured registry value for snapshots/reverts.</summary>
public sealed record RegistrySnapshotEntry(string Hive, string KeyPath, string ValueName, object? Value, bool Existed);

/// <summary>
/// Snapshot of every registry value an optimization touches. Reverts restore
/// exactly this state (including "value did not exist" -> delete), never a
/// hardcoded default (#5 of product spec: real rollback from snapshot).
/// </summary>
public sealed class OptimizationSnapshot
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public List<RegistrySnapshotEntry> Registry { get; } = new();

    public List<string> ServiceStartTypes { get; } = new();

    public List<string> RawNotes { get; } = new();
}
