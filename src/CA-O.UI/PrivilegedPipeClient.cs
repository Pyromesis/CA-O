using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using CAO.Shared;

namespace CAO.UI;

/// <summary>
/// UI-side client for the privileged service. The UI process stays
/// unprivileged; every mutation travels through the authenticated pipe with
/// typed parameters only (spec 5-7). No localhost HTTP anywhere.
/// </summary>
internal sealed class PrivilegedPipeClient
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    public async Task<PrivilegedOperationResponse?> SendAsync(
        PrivilegedOperation operation,
        string optimizationId,
        CancellationToken ct = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".", IpcConstants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(CallTimeout);
        await pipe.ConnectAsync(timeout.Token);

        var request = new PrivilegedOperationRequest
        {
            RequestId = Guid.NewGuid(),
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            Operation = operation,
            Parameters = new OperationParameters { OptimizationId = optimizationId },
        };
        await JsonSerializer.SerializeAsync(pipe, request, cancellationToken: timeout.Token);
        return await JsonSerializer.DeserializeAsync<PrivilegedOperationResponse>(pipe, cancellationToken: timeout.Token);
    }

    public Task<PrivilegedOperationResponse?> DetectAsync(string optimizationId, CancellationToken ct = default) =>
        SendAsync(PrivilegedOperation.DetectOptimization, optimizationId, ct);

    public Task<PrivilegedOperationResponse?> VerifyAsync(string optimizationId, CancellationToken ct = default) =>
        SendAsync(PrivilegedOperation.VerifyOptimization, optimizationId, ct);

    public Task<PrivilegedOperationResponse?> CaptureSnapshotAsync(string optimizationId, CancellationToken ct = default) =>
        SendAsync(PrivilegedOperation.CaptureSnapshot, optimizationId, ct);
}
