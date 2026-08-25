using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using CAO.Shared;

namespace CAO.Infrastructure.Networking;

public sealed class NetworkDiagnosticsProvider
{
    private const int AttemptsPerEndpoint = 3;

    public async Task<NetworkDiagnosticsReport> MeasureAsync(CancellationToken ct = default)
    {
        var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .Where(networkInterface => networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToArray();

        var interfaceNames = activeInterfaces.Select(networkInterface => networkInterface.Name).ToArray();
        var gateways = activeInterfaces
            .SelectMany(networkInterface => networkInterface.GetIPProperties().GatewayAddresses)
            .Select(gateway => gateway.Address)
            .Where(address => address is not null && !IPAddress.IsLoopback(address))
            .Distinct()
            .ToArray();
        var dnsServers = activeInterfaces
            .SelectMany(networkInterface => networkInterface.GetIPProperties().DnsAddresses)
            .Where(address => !IPAddress.IsLoopback(address))
            .Select(address => address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var measurements = new List<NetworkEndpointMeasurement>();
        foreach (var gateway in gateways)
        {
            measurements.Add(await MeasurePingAsync(gateway, "Gateway", ct));
        }

        foreach (var dnsServer in dnsServers)
        {
            measurements.Add(await MeasurePingAsync(IPAddress.Parse(dnsServer), "DNS", ct));
        }

        return new NetworkDiagnosticsReport(
            interfaceNames,
            dnsServers,
            measurements,
            DateTime.UtcNow);
    }

    private static async Task<NetworkEndpointMeasurement> MeasurePingAsync(
        IPAddress endpoint,
        string kind,
        CancellationToken ct)
    {
        using var ping = new Ping();
        var latencies = new List<double>();
        for (var attempt = 0; attempt < AttemptsPerEndpoint; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var reply = await ping.SendPingAsync(endpoint, 1000);
                stopwatch.Stop();
                if (reply.Status == IPStatus.Success)
                {
                    latencies.Add(reply.RoundtripTime > 0 ? reply.RoundtripTime : stopwatch.Elapsed.TotalMilliseconds);
                }
            }
            catch (PingException)
            {
                stopwatch.Stop();
            }
        }

        latencies.Sort();
        double? median = latencies.Count == 0 ? null : latencies[latencies.Count / 2];
        double? jitter = latencies.Count < 2
            ? null
            : latencies.Zip(latencies.Skip(1), (previous, current) => Math.Abs(current - previous)).Average();
        return new NetworkEndpointMeasurement(endpoint.ToString(), kind, AttemptsPerEndpoint, latencies.Count, median, jitter);
    }
}