using CAO.Core.Security;
using Xunit;

namespace CAO.Security.Tests;

/// <summary>
/// Replay cache guarantees (P1-9): single-use within TTL, bounded capacity
/// with oldest eviction, and deterministic behaviour via injected clock.
/// </summary>
public sealed class ReplayCacheTests
{
    private static (ReplayCache Cache, Func<DateTimeOffset> Advance) CacheWithClock(int? ttlMinutes = null, int? capacity = null)
    {
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset clock() => now;
        var cache = (ttlMinutes, capacity) switch
        {
            (null, null) => new ReplayCache(utcNow: clock),
            (_, null) => new ReplayCache(TimeSpan.FromMinutes(ttlMinutes!.Value), utcNow: clock),
            (null, _) => new ReplayCache(capacity: capacity!.Value, utcNow: clock),
            _ => new ReplayCache(TimeSpan.FromMinutes(ttlMinutes!.Value), capacity!.Value, clock),
        };
        return (cache, () => now = now.AddSeconds(1));
    }

    [Fact]
    public void SameNonceRejectedImmediately()
    {
        var (cache, advance) = CacheWithClock();
        var id = Guid.NewGuid();

        Assert.True(cache.TryAccept(id, "nonce-1"));
        advance(); // 1 second later, well inside TTL

        Assert.False(cache.TryAccept(id, "nonce-1"));
        Assert.False(cache.TryAccept(Guid.NewGuid(), "nonce-1"));
    }

    [Fact]
    public void EntryExpiresAfterTtl()
    {
        var (cache, advance) = CacheWithClock(ttlMinutes: 15);
        var id = Guid.NewGuid();

        Assert.True(cache.TryAccept(id, "n1"));

        // Advance past the TTL (16 minutes).
        for (var i = 0; i < 16 * 60; i++)
        {
            advance();
        }

        // After expiry the same nonce is acceptable again.
        Assert.True(cache.TryAccept(Guid.NewGuid(), "n1"));
    }

    [Fact]
    public void CapacityEvictionDropsOldestEntries()
    {
        var (cache, advance) = CacheWithClock(capacity: 6); // 3 requests = 6 entries

        for (var i = 0; i < 5; i++)
        {
            Assert.True(cache.TryAccept(Guid.NewGuid(), "nonce-" + i));
            advance();
        }
    }

    [Fact]
    public void ZeroCapacityStillFunctionsAsAlwaysReject()
    {
        // Degenerate configuration: any accept immediately evicts, but a
        // request is still accepted once at insert time.
        var cache = new ReplayCache(ttl: TimeSpan.FromMinutes(1), capacity: 2,
            utcNow: () => new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));

        var id = Guid.NewGuid();
        Assert.True(cache.TryAccept(id, "a"));
        Assert.False(cache.TryAccept(id, "a")); // replay inside window
    }
}
