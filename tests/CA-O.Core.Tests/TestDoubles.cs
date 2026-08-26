using CAO.Core.Abstractions;
using CAO.Core.Rollback;
using CAO.Shared;

namespace CAO.Core.Tests;

/// <summary>In-memory registry for deterministic transaction tests.</summary>
public sealed class MemoryRegistry : IRegistryAccessor
{
    public Dictionary<string, (object? Value, bool Existed, RegistryValueKind2 Kind)> Store { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    private static string Key(RegistryHive2 hive, string path, string name) =>
        $"{hive}:{path}\\{name}".ToLowerInvariant();

    public RegistryValueKind2 GetKind(RegistryHive2 hive, string keyPath, string valueName) =>
        GetValue(hive, keyPath, valueName) is string ? RegistryValueKind2.String : RegistryValueKind2.DWord;

    public object? GetValue(RegistryHive2 hive, string keyPath, string valueName) =>
        Store.TryGetValue(Key(hive, keyPath, valueName), out var entry) && entry.Existed ? entry.Value : null;

    public object? GetValueRaw(RegistryHive2 hive, string keyPath, string valueName, out RegistryValueKind2 kind)
    {
        if (Store.TryGetValue(Key(hive, keyPath, valueName), out var entry) && entry.Existed)
        {
            kind = entry.Kind;
            return entry.Value;
        }
        kind = RegistryValueKind2.None;
        return null;
    }

    public void SetValue(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
        Store[Key(hive, keyPath, valueName)] = (value, true, kind);

    public void SetValueRaw(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
        SetValue(hive, keyPath, valueName, value, kind);

    public bool DeleteValue(RegistryHive2 hive, string keyPath, string valueName) =>
        Store.Remove(Key(hive, keyPath, valueName));

    public IReadOnlyList<string> GetValueNames(RegistryHive2 hive, string keyPath) => [];
}

public sealed class MemorySnapshotStore : ISnapshotStore
{
    private readonly object _gate = new();
    public Dictionary<Guid, TransactionSnapshotRecord> Saved { get; } = new();

    public List<Guid> Deleted { get; } = [];

    public IEnumerable<Guid> ListIds() => Saved.Keys;

    public IReadOnlyList<TransactionSnapshotRecord> ListAll() =>
        Saved.Values.OrderBy(r => r.Manifest.TimestampUtc).ToList();

    public void Save(TransactionSnapshotRecord record) => Saved[record.Manifest.TransactionId] = record;

    public bool TryLoad(Guid transactionId, out TransactionSnapshotRecord? record)
    {
        if (Saved.TryGetValue(transactionId, out var found))
        {
            record = found;
            return true;
        }
        record = null;
        return false;
    }

    public bool TryLoadLatestForOptimization(string optimizationId, out TransactionSnapshotRecord? record)
    {
        record = Saved.Values
            .Where(r => r.Manifest.OptimizationId.Equals(optimizationId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Manifest.TimestampUtc)
            .FirstOrDefault();
        return record is not null;
    }

    public void Delete(Guid transactionId)
    {
        Deleted.Add(transactionId);
        Saved.Remove(transactionId);
    }

    // legacy API not exercised by transaction tests
    public bool TryLoadLegacy(string optimizationId, out OptimizationSnapshot? snapshot)
    {
        snapshot = null;
        return false;
    }

    public void DeleteLegacy(string optimizationId) { }

    public IReadOnlyList<string> ListLegacyIds() => Array.Empty<string>();
}

public sealed class MemoryHistory : IHistoryLogger
{
    public List<HistoryEntry> Entries { get; } = [];

    public void Log(HistoryEntry entry) => Entries.Add(entry);

    public IReadOnlyList<HistoryEntry> ReadLast(int maxEntries) =>
        Entries.TakeLast(maxEntries).ToList();
}

/// <summary>
/// Scriptable optimization used to drive every transaction branch without
/// touching Windows. Detect reads the memory registry so verification can
/// genuinely fail when apply "didn't take".
/// </summary>
public sealed class StubOptimization(OptimizationDefinition definition) : IOptimization
{
    public Func<SystemContext, PreconditionResult>? PreconditionOverride { get; set; }

    public Func<MemoryRegistry, OperationResult>? ApplyScript { get; set; }

    /// <summary>When true, VerifyAsync observes real registry state.</summary>
    public bool HonestVerification { get; set; } = true;

    public int ApplyCalls { get; private set; }
    public int RevertCalls { get; private set; }

    public OptimizationDefinition Definition { get; } = definition;

    public const string TestKey = @"SOFTWARE\CA-O\Test";
    public const string TestValue = "Enabled";

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var value = registry.GetValue(RegistryHive2.CurrentUser, TestKey, TestValue);
        if (value is null)
        {
            return OptimizationState.NotApplied;
        }
        return Equals(value, 1) ? OptimizationState.AppliedByCao : OptimizationState.AppliedManually;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        var existing = registry.GetValue(RegistryHive2.CurrentUser, TestKey, TestValue);
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            RegistryHive2.CurrentUser.ToString(), TestKey, TestValue, existing, Existed: existing is not null));
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        ApplyCalls++;
        if (ApplyScript is not null && context.Registry is MemoryRegistry memory)
        {
            return Task.FromResult(ApplyScript(memory));
        }
        context.Registry.SetValue(RegistryHive2.CurrentUser, TestKey, TestValue, 1, RegistryValueKind2.DWord);
        return Task.FromResult(OperationResult.Ok("aplicado"));
    }

    public async Task<OperationResult> RevertAsync(
        OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        RevertCalls++;
        foreach (var entry in snapshot.Registry)
        {
            if (entry.Existed && entry.Value is not null)
            {
                context.Registry.SetValue(Enum.Parse<RegistryHive2>(entry.Hive), entry.KeyPath,
                    entry.ValueName, entry.Value, RegistryValueKind2.DWord);
            }
            else
            {
                context.Registry.DeleteValue(Enum.Parse<RegistryHive2>(entry.Hive), entry.KeyPath, entry.ValueName);
            }
        }

        await Task.CompletedTask;
        return OperationResult.Ok("revertido");
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionOverride?.Invoke(context) ?? Compatibility.Rules.EvaluatePreconditions(Definition, context));
}
