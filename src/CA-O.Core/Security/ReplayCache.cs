using System.Collections.Concurrent;

namespace CAO.Core.Security;

/// <summary>
/// Bounded, TTL-based replay cache (P1-9): RequestId and nonce are accepted
/// exactly once within the TTL; capacity is hard-capped with oldest-entry
/// eviction so memory cannot grow without bound.
/// </summary>
public sealed class ReplayCache : IIpcReplayGuard
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;
    private readonly int _capacity;
    private DateTimeOffset _lastSweep;

    public ReplayCache(TimeSpan? ttl = null, int capacity = 10_000)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(15);
        _capacity = capacity;
        _lastSweep = DateTimeOffset.UtcNow;
    }

    public bool TryAccept(Guid requestId, string nonce)
    {
        MaybeSweep();

        var now = DateTimeOffset.UtcNow;
        var idKey = "id:" + requestId.ToString("N");
        var nonceKey = "n:" + nonce;

        if (_seen.ContainsKey(idKey) || _seen.ContainsKey(nonceKey))
        {
            return false;
        }

        // Two adds must succeed together for the request to be accepted.
        if (!_seen.TryAdd(idKey, now) || !_seen.TryAdd(nonceKey, now))
        {
            return false;
        }

        EvictOverflowIfAny();
        return true;
    }

    internal int Count => _seen.Count;

    private void EvictOverflowIfAny()
    {
        while (_seen.Count > _capacity)
        {
            string? oldest = null;
            var oldestTime = DateTimeOffset.MaxValue;
            foreach (var pair in _seen)
            {
                if (pair.Value < oldestTime)
                {
                    oldestTime = pair.Value;
                    oldest = pair.Key;
                }
            }
            if (oldest is null || !_seen.TryRemove(oldest, out _))
            {
                break;
            }
        }
    }

    /// <summary>Periodic sweep removes expired entries on activity.</summary>
    private void MaybeSweep()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastSweep < _ttl / 4)
        {
            return;
        }

        _lastSweep = now;
        foreach (var pair in _seen)
        {
            if (now - pair.Value > _ttl)
            {
                _seen.TryRemove(pair.Key, out _);
            }
        }
    }
}
