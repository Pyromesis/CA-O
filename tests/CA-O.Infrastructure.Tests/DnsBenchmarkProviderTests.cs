using System.Text;
using CAO.Infrastructure.Networking;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>DNS benchmark internals (spec 54): packet shape and statistics.</summary>
public sealed class DnsBenchmarkProviderTests
{
    [Fact]
    public void QueryPacketHasValidDnsHeader()
    {
        var query = DnsBenchmarkProvider.BuildQuery(0x1234, "cao-test-bench");

        Assert.True(query.Length >= 17 + 5);
        Assert.Equal(0x12, query[0]);
        Assert.Equal(0x34, query[1]);
        // Flags: standard query with recursion desired.
        Assert.Equal(0x01, query[2]);
        Assert.Equal(0x00, query[3]);
        // QDCOUNT = 1.
        Assert.Equal(0x00, query[4]);
        Assert.Equal(0x01, query[5]);

        // QTYPE=A and QCLASS=IN at the end.
        var tail = query[^4..];
        Assert.Equal([0x00, 0x01, 0x00, 0x01], tail);
    }

    [Fact]
    public void QueryEncodesLabelsCorrectly()
    {
        var query = DnsBenchmarkProvider.BuildQuery(1, "abc");

        // After 12-byte header: length-prefixed labels for abc.example.org.
        var ascii = Encoding.ASCII.GetString(query, 12, query.Length - 16);
        Assert.Contains("\u0003abc\u0007example\u0003org\u0000", ascii);
    }

    [Fact]
    public void PercentileInterpolatesWithinSortedSamples()
    {
        var samples = new List<double> { 10, 20, 30, 40 };

        Assert.Equal(10, DnsBenchmarkProvider.Percentile(samples, 0));
        Assert.Equal(25, DnsBenchmarkProvider.Percentile(samples, 0.5), 6);
        Assert.Equal(39.7, DnsBenchmarkProvider.Percentile(samples, 0.99), 3);
        Assert.Equal(40, DnsBenchmarkProvider.Percentile(samples, 1));
    }

    [Fact]
    public void PickBestRequiresMajoritySuccess()
    {
        var results = new List<DnsBenchmarkResult>
        {
            new("1.1.1.1", 8.0, 1.0, Attempts: 4, Successes: 1, Timeouts: 3),   // fast but flaky
            new("8.8.8.8", 15.0, 2.0, Attempts: 4, Successes: 4, Timeouts: 0), // reliable
        };

        var best = DnsBenchmarkProvider.PickBest(results);

        Assert.NotNull(best);
        Assert.Equal("8.8.8.8", best.Resolver);
    }
}
