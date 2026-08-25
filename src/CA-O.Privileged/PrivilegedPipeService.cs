using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using CAO.Shared;
using CAO.Core.Engine;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CAO.Privileged;

internal sealed class PrivilegedPipeService(
    ILogger<PrivilegedPipeService> logger,
    OptimizationEngine engine) : BackgroundService
{
    private const string PipeName = "CA-O.Privileged.v1";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<Guid, byte> _requestIds = new();
    private readonly ConcurrentDictionary<string, byte> _nonces = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var server = CreateServer();
            try
            {
                await server.WaitForConnectionAsync(stoppingToken);
                await HandleClientAsync(server, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "La conexión IPC terminó de forma inesperada.");
            }
        }
    }

    private static NamedPipeServerStream CreateServer()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            4096,
            4096,
            security);
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            var clientIdentity = pipe.GetImpersonationUserName();
            if (string.IsNullOrWhiteSpace(clientIdentity))
            {
                await JsonSerializer.SerializeAsync(
                    pipe,
                    PrivilegedOperationResponse.Rejected("Identidad IPC no disponible."),
                    cancellationToken: timeout.Token);
                return;
            }

            logger.LogInformation("Solicitud IPC recibida de {ClientIdentity}.", clientIdentity);
            var request = await JsonSerializer.DeserializeAsync<PrivilegedOperationRequest>(pipe, cancellationToken: timeout.Token);
            var response = await ValidateAndDispatchAsync(request, timeout.Token);
            await JsonSerializer.SerializeAsync(pipe, response, cancellationToken: timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("Solicitud IPC cancelada o excedió el tiempo límite.");
        }
        catch (JsonException)
        {
            await JsonSerializer.SerializeAsync(pipe, PrivilegedOperationResponse.Rejected("JSON inválido."), cancellationToken: stoppingToken);
        }
    }

    private async Task<PrivilegedOperationResponse> ValidateAndDispatchAsync(
        PrivilegedOperationRequest? request,
        CancellationToken ct)
    {
        var error = "Solicitud inválida.";
        if (request is null || !PrivilegedOperationValidator.TryValidate(request, out error))
        {
            return PrivilegedOperationResponse.Rejected(error);
        }

        if (!_requestIds.TryAdd(request.RequestId, 0) || !_nonces.TryAdd(request.Nonce, 0))
        {
            return PrivilegedOperationResponse.Rejected("Solicitud repetida.");
        }

        try
        {
            return request.Operation switch
            {
                PrivilegedOperation.ApplyOptimization => FromResult(await engine.ApplyAsync(request.Parameters.OptimizationId!, ct)),
                PrivilegedOperation.RevertOptimization => FromResult(await engine.RevertAsync(request.Parameters.OptimizationId!, ct)),
                PrivilegedOperation.DetectOptimization => new PrivilegedOperationResponse(
                    true,
                    null,
                    engine.Detect(request.Parameters.OptimizationId!).ToString()),
                _ => PrivilegedOperationResponse.Rejected("Operación no disponible en esta versión del servicio."),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ejecutando la operación IPC {Operation}.", request.Operation);
            return PrivilegedOperationResponse.Rejected("La operación falló.");
        }
    }

    private static PrivilegedOperationResponse FromResult(CAO.Core.Abstractions.OperationResult result) =>
        new(result.Success, result.Error, result.MessageEs);
}

internal sealed record PrivilegedOperationResponse(bool Accepted, string? Error, string? MessageEs = null)
{
    public static PrivilegedOperationResponse Rejected(string error) => new(false, error);
}