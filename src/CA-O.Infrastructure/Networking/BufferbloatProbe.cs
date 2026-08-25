using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace CAO.Infrastructure.Networking;

/// <summary>Measured bufferbloat numbers for one direction.</summary>
public sealed record BufferbloatMeasurement(
    double IdleMedianMs,
    double? LoadedMedianMs,
    double? LatencyIncreasePercent,
    int LossUnderLoad);

public sealed record BufferbloatReport(
    BufferbloatMeasurement Download,
    BufferbloatMeasurement Upload,
    DateTime TimestampUtc);

/// <summary>
/// Bufferbloat diagnostics (spec 53): compares idle latency with latency
/// under concurrent transfer load. Recommends SQM/QoS/router changes when
/// the increase is significant. Uses public speed endpoints; every value is
/// measured, never estimated.
/// </summary>
public sealed class BufferbloatProbe
{
    private const string DownloadUrl = "https://speed.cloudflare.com/__down?bytes=20000000";
    private const string UploadUrl = "https://speed.cloudflare.com/__up";
    private const int PingAttempts = 6;

    public async Task<BufferbloatReport> MeasureAsync(CancellationToken ct = default)
    {
        var gateway = FindGateway();
        if (gateway is null)
        {
            throw new InvalidOperationException("No hay puerta de enlace activa para medir bufferbloat.");
        }

        var idle = await MeasureLatencyAsync(gateway, ct);

        var downloadLoaded = await MeasureLoadedAsync(gateway, loadDownload: true, ct);
        var uploadLoaded = await MeasureLoadedAsync(gateway, loadDownload: false, ct);

        return new BufferbloatReport(
            ToMeasurement(idle, downloadLoaded),
            ToMeasurement(idle, uploadLoaded),
            DateTime.UtcNow);
    }

    private static BufferbloatMeasurement ToMeasurement(double idleMedian, (double? Median, int Loss) loaded) => new(
        idleMedian,
        loaded.Median,
        loaded.Median is null ? null : Math.Round((loaded.Median.Value - idleMedian) / idleMedian * 100, 1),
        loaded.Loss);

    private static IPAddress? FindGateway()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().GatewayAddresses)
                .Select(gateway => gateway.Address)
                .FirstOrDefault(address => address is not null && !IPAddress.IsLoopback(address));
        }
        catch
        {
            return null;
        }
    }

    private static async Task<double> MeasureLatencyAsync(IPAddress target, CancellationToken ct)
    {
        using var ping = new Ping();
        var samples = new List<double>();
        for (var attempt = 0; attempt < PingAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(target, 1000);
                if (reply.Status == IPStatus.Success && reply.RoundtripTime > 0)
                {
                    samples.Add(reply.RoundtripTime);
                }
            }
            catch (PingException)
            {
            }

            await Task.Delay(120, ct);
        }

        if (samples.Count == 0)
        {
            throw new InvalidOperationException("La puerta de enlace no responde a ICMP.");
        }

        samples.Sort();
        return DnsBenchmarkProvider.Percentile(samples, 0.5);
    }

    private async Task<(double? Median, int Loss)> MeasureLoadedAsync(
        IPAddress gateway, bool loadDownload, CancellationToken ct)
    {
        using var loadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var loadTask = loadDownload
            ? GenerateDownloadLoad(loadCts.Token)
            : GenerateUploadLoad(loadCts.Token);
        _ = loadTask.ContinueWith(_ => { }, TaskScheduler.Default);

        try
        {
            // Give the transfer a moment to saturate before sampling.
            await Task.Delay(700, ct);
            using var ping = new Ping();
            var samples = new List<double>();
            var losses = 0;

            for (var attempt = 0; attempt < PingAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var reply = await ping.SendPingAsync(gateway, 1000);
                    if (reply.Status == IPStatus.Success && reply.RoundtripTime > 0)
                    {
                        samples.Add(reply.RoundtripTime);
                    }
                    else
                    {
                        losses++;
                    }
                }
                catch (PingException)
                {
                    losses++;
                }

                await Task.Delay(120, ct);
            }

            double? median = samples.Count == 0 ? null :
                DnsBenchmarkProvider.Percentile(samples.OrderBy(value => value).ToList(), 0.5);
            return (median, losses);
        }
        finally
        {
            loadCts.Cancel();
            try { await loadTask; } catch (OperationCanceledException) { }
        }
    }

    private static async Task GenerateDownloadLoad(CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            await using var stream = await client.GetStreamAsync(DownloadUrl, ct);
            var buffer = new byte[64 * 1024];
            while (!ct.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(buffer, ct);
            }
        }
        catch when (ct.IsCancellationRequested)
        {
        }
        catch
        {
            // Load generation failure must not crash diagnostics.
        }
    }

    private static async Task GenerateUploadLoad(CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            using var content = new ByteArrayContent(new byte[4 * 1024 * 1024]);
            while (!ct.IsCancellationRequested)
            {
                await client.PostAsync(UploadUrl, content, ct);
            }
        }
        catch when (ct.IsCancellationRequested)
        {
        }
        catch
        {
        }
    }
}
