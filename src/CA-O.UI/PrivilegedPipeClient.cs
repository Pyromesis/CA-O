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
internal sealed class PrivilegedPipeClient
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<IpcResponse?> SendAsync(
        PrivilegedOperationKind operation,
        string optimizationId,
        CancellationToken ct = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".", IpcConstants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(CallTimeout);
        await pipe.ConnectAsync(timeout.Token);

        var request = new IpcRequest(
            ProtocolVersion: IpcProtocol.Version,
            RequestId: Guid.NewGuid(),
            Nonce: Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            CreatedAtUtc: DateTime.UtcNow,
            Operation: operation,
            Payload: new OptimizationTargetPayload(optimizationId));

        await JsonSerializer.SerializeAsync(pipe, request, JsonOptions, timeout.Token);
        return await JsonSerializer.DeserializeAsync<IpcResponse>(pipe, JsonOptions, timeout.Token);
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

    /// <summary>Legacy response shape used by pages; maps v2 codes through.</summary>
    private static IpcResponse? Map(IpcResponse? response) => response;
}
