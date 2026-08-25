using System.Collections.Concurrent;

namespace CAO.Core.Rollback;

/// <summary>
/// Logical resource a transaction must lock before mutating (FASE 15),
/// e.g. Registry:HKLM\..., Service:WSearch, PowerPlan:System.
/// </summary>
public sealed record ResourceKey(string Kind, string Name)
{
    public override string ToString() => $"{Kind}:{Name}";

    public static ResourceKey ForOptimization(string optimizationId) => new("Optimization", optimizationId);

    public static ResourceKey Service(string serviceName) => new("Service", serviceName);

    public static ResourceKey PowerPlan() => new("PowerPlan", "System");

    public static ResourceKey BootConfiguration() => new("Boot", "BCD");
}

/// <summary>
/// Cross-transaction resource locking (FASE 15): two transactions touching
/// the same resource are serialized; keys are acquired in canonical order to
/// avoid deadlocks, all-or-nothing with a timeout. The process-wide Shared
/// instance is used by the privileged host and by tests.
/// </summary>
public sealed class ResourceLockManager
{
    public static ResourceLockManager Shared { get; } = new();

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <summary>Acquires every key or none; releases when the lease is disposed.</summary>
    public async Task<IAsyncDisposable> AcquireAsync(
        IEnumerable<ResourceKey> keys,
        CancellationToken ct = default)
    {
        var ordered = keys
            .Select(key => key.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var acquired = new List<SemaphoreSlim>(ordered.Length);
        try
        {
            foreach (var name in ordered)
            {
                var semaphore = _locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
                if (!await semaphore.WaitAsync(TimeSpan.FromSeconds(30), ct))
                {
                    throw new TimeoutException($"CAO-TXN-007: timeout esperando el recurso '{name}'.");
                }
                acquired.Add(semaphore);
            }
        }
        catch
        {
            Release(acquired);
            throw;
        }

        return new Lease(acquired);
    }

    /// <summary>Number of currently held leases for tests/diagnostics.</summary>
    internal int HeldCountForTesting(ResourceKey key) =>
        (int)_locks.GetOrAdd(key.ToString(), _ => new SemaphoreSlim(1, 1)).CurrentCount;

    private static void Release(List<SemaphoreSlim> acquired)
    {
        foreach (var semaphore in acquired)
        {
            semaphore.Release();
        }
    }

    private sealed class Lease(List<SemaphoreSlim> semaphores) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            Release(semaphores);
            return ValueTask.CompletedTask;
        }
    }
}
