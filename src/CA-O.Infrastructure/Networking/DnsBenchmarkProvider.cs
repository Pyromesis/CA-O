using System.Net.Sockets;
using System.Text;

namespace CAO.Infrastructure.Networking;

/// <summary>Outcome of benchmarking one DNS resolver.</summary>
public sealed record DnsBenchmarkResult(
    string Resolver,
    double? MedianLatencyMs,
    double? JitterMs,
    int Attempts,
    int Successes,
    int Timeouts);

/// <summary>
/// Measures real recursive resolution latency against several resolvers and
/// recommends by measurement instead of imposing a vendor (spec 54).
/// Uses a random subdomain so answers exercise full recursion (NXDOMAIN),
/// keeping cache effects equal across resolvers.
/// </summary>
public sealed class DnsBenchmarkProvider
{
    private static readonly string[] PublicResolvers =
    [
        "1.1.1.1", // Cloudflare
        "8.8.8.8", // Google
        "9.9.9.9", // Quad9
    ];

    private const ushort DefaultPort = 53;
    private const int AttemptsPerResolver = 4;
    private static readonly TimeSpan PerQueryTimeout = TimeSpan.FromMilliseconds(1200);

    public async Task<IReadOnlyList<DnsBenchmarkResult>> BenchmarkAsync(
        IReadOnlyList<string>? extraResolvers = null,
        CancellationToken ct = default)
    {
        var resolvers = CollectResolvers(extraResolvers);
        var results = new List<DnsBenchmarkResult>();

        foreach (var resolver in resolvers)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await MeasureResolverAsync(resolver, ct));
        }

        return results;
    }

    /// <summary>Best measured resolver; null when nothing answered.</summary>
    public static DnsBenchmarkResult? PickBest(IReadOnlyList<DnsBenchmarkResult> results) =>
        results
            .Where(result => result.MedianLatencyMs is not null && result.Successes >= Math.Max(1, result.Attempts / 2))
            .OrderBy(result => result.MedianLatencyMs)
            .FirstOrDefault();

    private async Task<DnsBenchmarkResult> MeasureResolverAsync(string resolverIp, CancellationToken ct)
    {
        var latencies = new List<double>();
        var successes = 0;
        var timeouts = 0;

        for (var attempt = 0; attempt < AttemptsPerResolver; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var queryId = Random.Shared.Next(ushort.MaxValue + 1);
            var label = $"cao-{Random.Shared.Next(1_000_000)}-bench";
            var packet = BuildQuery(queryId, label);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var udp = new UdpClient(resolverIp, DefaultPort);
                await udp.SendAsync(packet, ct);
                var receiveTask = udp.ReceiveAsync(ct).AsTask();
                var completed = await Task.WhenAny(receiveTask, Task.Delay(PerQueryTimeout, ct));
                sw.Stop();

                if (completed != receiveTask)
                {
                    timeouts++;
                    continue;
                }

                var response = await receiveTask;
                var payload = response.Buffer;
                if (payload.Length >= 12 && payload[0] == packet[0] && payload[1] == packet[1])
                {
                    latencies.Add(sw.Elapsed.TotalMilliseconds);
                    successes++;
                }
                else
                {
                    timeouts++;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (SocketException)
            {
                sw.Stop();
                timeouts++;
            }
        }

        latencies.Sort();
        double? median = latencies.Count == 0 ? null : Percentile(latencies, 0.5);
        double? jitter = latencies.Count < 2 ? null :
            latencies.Zip(latencies.Skip(1), (a, b) => Math.Abs(b - a)).Average();

        return new DnsBenchmarkResult(resolverIp, median, jitter, AttemptsPerResolver, successes, timeouts);
    }

    private static IReadOnlyList<string> CollectResolvers(IReadOnlyList<string>? extras)
    {
        var set = new List<string>();
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    continue;
                }

                foreach (var dns in nic.GetIPProperties().DnsAddresses)
                {
                    if (!System.Net.IPAddress.IsLoopback(dns) &&
                        dns.AddressFamily == AddressFamily.InterNetwork)
                    {
                        set.Add(dns.ToString());
                    }
                }
            }
        }
        catch
        {
            // Fall back to public resolvers only.
        }

        set.AddRange(PublicResolvers);
        if (extras is not null)
        {
            set.AddRange(extras);
        }

        return set.Distinct(StringComparer.Ordinal).ToList();
    }

    internal static byte[] BuildQuery(int id, string label)
    {
        // Header: id, flags=0x0100 (recursion desired), qdcount=1.
        var query = new List<byte>(64)
        {
            (byte)(id >> 8), (byte)id,
            0x01, 0x00,
            0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };

        // QNAME as length-prefixed labels terminated by the root zero byte.
        foreach (var part in $"{label}.example.org".Split('.'))
        {
            query.Add((byte)part.Length);
            query.AddRange(Encoding.ASCII.GetBytes(part));
        }
        query.Add(0);

        // QTYPE=A(1), QCLASS=IN(1)
        query.AddRange([0x00, 0x01, 0x00, 0x01]);
        return [.. query];
    }

    internal static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            throw new ArgumentException("Empty sample.", nameof(sortedValues));
        }

        var position = percentile * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, sortedValues.Count - 1);
        var fraction = position - lower;
        return sortedValues[lower] * (1 - fraction) + sortedValues[upper] * fraction;
    }
}
