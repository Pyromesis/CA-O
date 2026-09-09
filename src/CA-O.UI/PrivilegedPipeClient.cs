using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using CAO.Shared;
using CAO.Shared.IPC;

namespace CAO.UI;

/// <summary>
/// UI-side client for the privileged service, protocol v2 (FASE 3). The UI
/// process stays unprivileged; every mutation travels through the
/// authenticated pipe as a typed payload with request id + nonce + timestamp.
/// Structured error codes (CAO-XXX-nnn) surface rejections verbatim.
/// </summary>
public sealed class PrivilegedPipeClient
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Task<IpcResponse?> SendAsync(
        PrivilegedOperationKind operation,
        string optimizationId,
        CancellationToken ct = default)
    {
        // Soporte SetDns con payload tipado SetDnsPayload
        ITypedPayload payload;
        if (operation == PrivilegedOperationKind.SetDns)
        {
            // optimizationId lleva "interfaceName|dnsIp" para SetDns
            var parts = optimizationId.Split('|', 2);
            var iface = parts.Length > 0 ? parts[0] : "";
            var dns = parts.Length > 1 ? parts[1] : "";
            payload = new SetDnsPayload(iface, dns);
        }
        else if (operation == PrivilegedOperationKind.FixDriver)
        {
            // optimizationId lleva "instanceId|action" para FixDriver
            var parts = optimizationId.Split('|', 2);
            var instanceId = parts.Length > 0 ? parts[0] : "";
            var action = parts.Length > 1 ? parts[1] : "";
            payload = new DriverFixPayload(instanceId, action);
        }
        else
        {
            payload = operation switch
            {
                PrivilegedOperationKind.ApplyOptimization => new ApplyOptimizationPayload(optimizationId),
                PrivilegedOperationKind.RevertOptimization => new RevertOptimizationPayload(optimizationId),
                PrivilegedOperationKind.DetectOptimization => new DetectOptimizationPayload(optimizationId),
                PrivilegedOperationKind.VerifyOptimization => new VerifyOptimizationPayload(optimizationId),
                PrivilegedOperationKind.CaptureSnapshot => new CaptureSnapshotPayload(optimizationId),
                PrivilegedOperationKind.Ping => new PingPayload(),
                PrivilegedOperationKind.GetServiceStatus => new GetServiceStatusPayload(),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }

        return SendPayloadAsync(operation, payload, ct);
    }

    public Task<IpcResponse?> FixDriverAsync(string instanceId, string action, CancellationToken ct = default) =>
        SendAsync(PrivilegedOperationKind.FixDriver, $"{instanceId}|{action}", ct);

    public Task<IpcResponse?> InstallDriverAsync(string infPath, string instanceId, CancellationToken ct = default) =>
        SendPayloadAsync(PrivilegedOperationKind.InstallDriver, new InstallDriverPayload(infPath, instanceId), ct);

    public Task<IpcResponse?> SetTimerResolutionAsync(uint resolution100Ns, CancellationToken ct = default) =>
        SendPayloadAsync(PrivilegedOperationKind.SetTimerResolution, new SetTimerResolutionPayload(resolution100Ns), ct);

    public async Task<IpcResponse?> SendPayloadAsync(
        PrivilegedOperationKind operation,
        ITypedPayload payload,
        CancellationToken ct = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".", IpcConstants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        // La conexión sí tiene techo corto (10 s); la RESPUESTA respeta el
        // timeout del llamante (las operaciones pesadas necesitan minutos).
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(CallTimeout);
        try
        {
            await pipe.ConnectAsync(connectCts.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcTimeout, "Servicio no disponible: tiempo de espera agotado (CAO-IPC-004). Verifique que CA-O Privileged Service esté instalado e iniciado.");
        }
        catch (IOException)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcPipeNotFound, "Servicio no disponible: pipe no encontrado (CAO-IPC-004). Instale/inicie el servicio privilegiado con scripts/install-privileged-service.ps1.");
        }
        catch (TimeoutException)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcTimeout, "Servicio no disponible: timeout al conectar (CAO-IPC-004).");
        }

        var request = new IpcRequest(
            ProtocolVersion: IpcProtocol.Version,
            RequestId: Guid.NewGuid(),
            Nonce: Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            CreatedAtUtc: DateTime.UtcNow,
            Operation: operation,
            Payload: payload);

        try
        {
            // Frameo por línea: 1 JSON por línea + \n, evita bloqueos de Byte vs Message
            var json = JsonSerializer.Serialize(request, JsonOptions);
            // Validar tamaño antes de enviar
            if (System.Text.Encoding.UTF8.GetByteCount(json) > IpcProtocol.MaxRequestBytes)
                return IpcResponse.Rejected(ErrorCodes.IpcRequestTooLarge, "Solicitud excede 64KB.");
            using (var writer = new StreamWriter(pipe, System.Text.Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true })
            {
                await writer.WriteLineAsync(json.AsMemory(), ct);
                writer.Flush();
            }
            // Leer una línea de respuesta con el timeout del llamante (no el de conexión)
            string? line;
            using (var reader = new StreamReader(pipe, System.Text.Encoding.UTF8, false, 1024, leaveOpen: true))
            {
                line = await reader.ReadLineAsync(ct);
            }
            if (string.IsNullOrWhiteSpace(line))
                return IpcResponse.Rejected(ErrorCodes.IpcMalformedRequest, "Respuesta vacía del servicio.");
            var response = JsonSerializer.Deserialize<IpcResponse>(line, JsonOptions);
            return response ?? IpcResponse.Rejected(ErrorCodes.IpcMalformedRequest, "Respuesta vacía del servicio.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcTimeout, "Servicio no respondió a tiempo (CAO-IPC-007).");
        }
        catch (IOException ex)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcPipeNotFound, $"Pipe roto: {ex.Message} (CAO-IPC-008).");
        }
        catch (JsonException ex)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcMalformedRequest, $"Respuesta JSON inválida: {ex.Message} (CAO-IPC-002).");
        }
        catch (Exception ex)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcMalformedRequest, $"Error IPC: {ex.GetType().Name} — {ex.Message}");
        }
    }

    public Task<IpcResponse?> DetectAsync(string optimizationId, CancellationToken ct = default) =>
        SendAsync(PrivilegedOperationKind.DetectOptimization, optimizationId, ct);

    public Task<IpcResponse?> VerifyAsync(string optimizationId, CancellationToken ct = default) =>
        SendAsync(PrivilegedOperationKind.VerifyOptimization, optimizationId, ct);

    public Task<IpcResponse?> CaptureSnapshotAsync(string optimizationId, CancellationToken ct = default) =>
        SendAsync(PrivilegedOperationKind.CaptureSnapshot, optimizationId, ct);

    public async Task<IpcResponse?> ApplyAsync(string optimizationId, CancellationToken ct = default) =>
        Map(await SendAsync(PrivilegedOperationKind.ApplyOptimization, optimizationId, ct));

    public async Task<IpcResponse?> RevertAsync(string optimizationId, CancellationToken ct = default) =>
        Map(await SendAsync(PrivilegedOperationKind.RevertOptimization, optimizationId, ct));

    public Task<IpcResponse?> PingAsync(CancellationToken ct = default) =>
        SendAsync(PrivilegedOperationKind.Ping, string.Empty, ct);

    public Task<IpcResponse?> GetServiceStatusAsync(CancellationToken ct = default) =>
        SendAsync(PrivilegedOperationKind.GetServiceStatus, string.Empty, ct);

    public Task<IpcResponse?> SetDnsAsync(string interfaceName, string dnsIp, CancellationToken ct = default) =>
        SendAsync(PrivilegedOperationKind.SetDns, $"{interfaceName}|{dnsIp}", ct);

    /// <summary>Legacy response shape used by pages; maps v2 codes through.</summary>
    private static IpcResponse? Map(IpcResponse? response) => response;
}
