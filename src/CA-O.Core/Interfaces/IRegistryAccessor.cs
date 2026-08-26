using CAO.Shared;

namespace CAO.Core.Abstractions;

/// <summary>
/// Registry access abstraction so optimization logic can be unit-tested
/// against an in-memory store. Supports EXACT kind capture (FASE 8): raw
/// reads return the true kind with unexpanded/uncoerced data.
/// </summary>
public interface IRegistryAccessor
{
    RegistryValueKind2 GetKind(RegistryHive2 hive, string keyPath, string valueName);

    object? GetValue(RegistryHive2 hive, string keyPath, string valueName);

    /// <summary>
    /// Exact read: value plus its real kind (REG_BINARY as byte[],
    /// REG_MULTI_SZ as string[], REG_EXPAND_SZ UNEXPANDED).
    /// </summary>
    object? GetValueRaw(RegistryHive2 hive, string keyPath, string valueName, out RegistryValueKind2 kind);

    /// <summary>Writes a value creating intermediate keys as needed.</summary>
    void SetValue(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind);

    /// <summary>
    /// Exact write: persists the declared kind verbatim (FASE 8). Used by
    /// rollback so a captured REG_EXPAND_SZ/BINARY/MULTI_SZ is restored
    /// byte-for-byte / element-for-element.
    /// </summary>
    void SetValueRaw(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind);

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

/// <summary>Full registry kinds supported by the snapshot engine (FASE 8).</summary>
public enum RegistryValueKind2
{
    None = 0,
    String = 1,          // REG_SZ
    ExpandString = 2,    // REG_EXPAND_SZ
    Binary = 3,          // REG_BINARY
    DWord = 4,           // REG_DWORD
    MultiString = 5,     // REG_MULTI_SZ
    QWord = 6,           // REG_QWORD
}

public static class RegistryKind2WireExtensions
{
    /// <summary>Canonical wire name used in snapshot manifests (FASE 8).</summary>
    public static string ToWire(this RegistryValueKind2 kind) => kind switch
    {
        RegistryValueKind2.None => "REG_NONE",
        RegistryValueKind2.String => "REG_SZ",
        RegistryValueKind2.ExpandString => "REG_EXPAND_SZ",
        RegistryValueKind2.Binary => "REG_BINARY",
        RegistryValueKind2.DWord => "REG_DWORD",
        RegistryValueKind2.MultiString => "REG_MULTI_SZ",
        RegistryValueKind2.QWord => "REG_QWORD",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static bool TryParseWire(string wire, out RegistryValueKind2 kind)
    {
        switch (wire)
        {
            case "REG_NONE": kind = RegistryValueKind2.None; return true;
            case "REG_SZ": kind = RegistryValueKind2.String; return true;
            case "REG_EXPAND_SZ": kind = RegistryValueKind2.ExpandString; return true;
            case "REG_BINARY": kind = RegistryValueKind2.Binary; return true;
            case "REG_DWORD": kind = RegistryValueKind2.DWord; return true;
            case "REG_MULTI_SZ": kind = RegistryValueKind2.MultiString; return true;
            case "REG_QWORD": kind = RegistryValueKind2.QWord; return true;
            default: kind = RegistryValueKind2.None; return false;
        }
    }

    public static RegistryValueKind2 ParseWire(string wire) =>
        TryParseWire(wire, out var kind)
            ? kind
            : throw new FormatException("Unknown registry kind '" + wire + "'.");
}

public static class RegistrySnapshotExtensions
{
    /// <summary>Semantic-exact comparison used to verify rollbacks (FASE 9).</summary>
    public static bool SemanticallyEquals(this RegistrySnapshotEntry entry, RegistrySnapshotEntry other) =>
        entry.Existed == other.Existed &&
        entry.Kind == other.Kind &&
        (!entry.Existed || ValuesEqual(Normalize(entry.Value), Normalize(other.Value)));

    private static object? Normalize(object? value) => value switch
    {
        int i => (long)i,
        uint u => (long)u,
        long l => l,
        byte[] bytes => Convert.ToBase64String(bytes),
        System.Collections.Generic.IEnumerable<string> multi => string.Join("\u0001", multi),
        _ => value?.ToString(),
    };

    private static bool ValuesEqual(object? a, object? b) =>
        a is null && b is null || Equals(a, b);
}

/// <summary>A single captured registry value for snapshots/reverts.</summary>
public sealed record RegistrySnapshotEntry(string Hive, string KeyPath, string ValueName, object? Value, bool Existed)
{
    /// <summary>Exact original kind; never inferred on rollback (FASE 8).</summary>
    public RegistryValueKind2 Kind { get; init; } = RegistryValueKind2.DWord;
}

/// <summary>
/// Snapshot of every registry value an optimization touches. Reverts restore
/// exactly this state (including "value did not exist" -> delete), never a
/// hardcoded default.
/// </summary>
public sealed class OptimizationSnapshot
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public List<RegistrySnapshotEntry> Registry { get; } = new();

    public List<string> ServiceStartTypes { get; } = new();

    public List<string> RawNotes { get; } = new();
}
