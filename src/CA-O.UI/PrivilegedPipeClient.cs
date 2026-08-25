using System.IO.Pipes;
using System.Text.Json;
using CAO.Shared;

namespace CAO.UI;

internal sealed class PrivilegedPipeClient
{
    private const string PipeName = "CA-O.Privileged.v1";

    public async Task<PrivilegedOperationResponse?> DetectAsync(string optimizationId, CancellationToken ct = default)
    {
        await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await pipe.ConnectAsync(timeout.Token);

        var request = new PrivilegedOperationRequest
        {
            RequestId = Guid.NewGuid(),
            Nonce = Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            Operation = PrivilegedOperation.DetectOptimization,
            Parameters = new OperationParameters { OptimizationId = optimizationId },
        };
        await JsonSerializer.SerializeAsync(pipe, request, cancellationToken: timeout.Token);
        return await JsonSerializer.DeserializeAsync<PrivilegedOperationResponse>(pipe, cancellationToken: timeout.Token);
    }
}

internal sealed record PrivilegedOperationResponse(bool Accepted, string? Error, string? MessageEs = null);