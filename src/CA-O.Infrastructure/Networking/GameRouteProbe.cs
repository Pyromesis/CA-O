using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CAO.Core.GameNetwork;

namespace CAO.Infrastructure.Networking;

/// <summary>
/// Mediciones de red para juegos, sin privilegios y sin tocar nada:
/// RTT por conexión TCP (SYN → SYN-ACK = un viaje de ida y vuelta; funciona
/// aunque el destino bloquee ping) y traceroute por ICMP con TTL creciente.
/// La resolución DNS se hace una vez y fuera del cronómetro.
/// </summary>
public sealed class GameRouteProbe
{
    public const int ConnectTimeoutMs = 1500;

    public static async Task<IPAddress?> ResolveAsync(string host, CancellationToken ct)
    {
        if (IPAddress.TryParse(host, out var literal)) return literal;
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
            return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();
        }
        catch (SocketException)
        {
            return null;
        }
    }

    /// <summary>Una muestra de RTT por TCP; null si no conecta dentro del tiempo límite.</summary>
    public static async Task<double?> TcpRttAsync(IPAddress address, int port, CancellationToken ct)
    {
        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ConnectTimeoutMs);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
        finally
        {
            try { socket.Close(0); } catch { }
        }
    }

    /// <summary>N muestras espaciadas; la primera conexión calienta la caché ARP/ruta y se descarta.</summary>
    public static async Task<LatencyStats> MeasureAsync(
        IPAddress address, int port, int samples, int intervalMs, CancellationToken ct)
    {
        await TcpRttAsync(address, port, ct).ConfigureAwait(false);
        var values = new List<double?>(samples);
        for (var i = 0; i < samples; i++)
        {
            ct.ThrowIfCancellationRequested();
            values.Add(await TcpRttAsync(address, port, ct).ConfigureAwait(false));
            if (i < samples - 1) await Task.Delay(intervalMs, ct).ConfigureAwait(false);
        }
        return LatencyStats.From(values);
    }

    /// <summary>
    /// Traceroute: por cada TTL manda <paramref name="probesPerHop"/> pings.
    /// Se detiene al llegar al destino o tras <paramref name="maxHops"/>.
    /// </summary>
    public static async Task<IReadOnlyList<RouteHop>> TraceAsync(
        IPAddress destination, int maxHops, int probesPerHop, IProgress<RouteHop>? progress, CancellationToken ct)
    {
        var hops = new List<RouteHop>();
        var payload = new byte[32];
        using var ping = new Ping();
        for (var ttl = 1; ttl <= maxHops; ttl++)
        {
            ct.ThrowIfCancellationRequested();
            string? address = null;
            var reachedDestination = false;
            var samples = new List<double?>(probesPerHop);
            for (var probe = 0; probe < probesPerHop; probe++)
            {
                ct.ThrowIfCancellationRequested();
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var reply = await ping.SendPingAsync(destination, TimeSpan.FromMilliseconds(1200), payload,
                        new PingOptions(ttl, dontFragment: true), ct).ConfigureAwait(false);
                    stopwatch.Stop();
                    if (reply.Status is IPStatus.Success or IPStatus.TtlExpired or IPStatus.TimeExceeded)
                    {
                        address ??= reply.Address?.ToString();
                        reachedDestination |= reply.Status == IPStatus.Success;
                        // RoundtripTime viene a 0 en respuestas TtlExpired: se usa el cronómetro.
                        samples.Add(Math.Round(reply.RoundtripTime > 0 ? reply.RoundtripTime : stopwatch.Elapsed.TotalMilliseconds, 1));
                    }
                    else
                    {
                        samples.Add(null);
                    }
                }
                catch (PingException)
                {
                    samples.Add(null);
                }
            }

            var hop = new RouteHop(ttl, address, LatencyStats.From(samples), reachedDestination);
            hops.Add(hop);
            progress?.Report(hop);
            if (reachedDestination) break;
        }
        return hops;
    }

    /// <summary>Tipo del adaptador con la ruta por defecto (para avisar de Wi-Fi). Nunca lanza.</summary>
    public static string ActiveLinkDescription()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            n.GetIPProperties().GatewayAddresses.Any(g => g.Address is not null && !IPAddress.Any.Equals(g.Address)))
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();
            if (nic is null) return "sin conexión activa";
            var kind = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "Cable";
            var speed = nic.Speed > 0 ? $" · {nic.Speed / 1_000_000} Mbps" : string.Empty;
            return $"{kind} · {nic.Name}{speed}";
        }
        catch
        {
            return "desconocida";
        }
    }

    public static bool IsWifiActive()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces().Any(n =>
                n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                n.GetIPProperties().GatewayAddresses.Any(g => g.Address is not null && !IPAddress.Any.Equals(g.Address)));
        }
        catch
        {
            return false;
        }
    }
}
