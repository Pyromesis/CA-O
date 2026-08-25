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

/// <summary>
/// Authenticated named-pipe host for privileged operations (spec 5-7):
/// restrictive ACL, identity check, schema validation, operation allowlist,
/// nonce replay protection, per-connection timeout and full audit logging.
/// The service only executes strongly-typed operations from the catalog;
/// arbitrary commands are impossible by construction.
/// </summary>
internal sealed class PrivilegedPipeService(
    ILogger<PrivilegedPipeService> logger,
    OptimizationEngine engine) : BackgroundService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<Guid, byte> _requestIds = new();
    private readonly ConcurrentDictionary<string, byte> _nonces = new(StringComparer.Ordinal);

    /// <summary>Operations this build is allowed to execute (allowlist).</summary>
    private static readonly IReadOnlySet<PrivilegedOperation> AllowedOperations =
        new HashSet<PrivilegedOperation>
        {
            PrivilegedOperation.ApplyOptimization,
            PrivilegedOperation.RevertOptimization,
            PrivilegedOperation.DetectOptimization,
            PrivilegedOperation.CaptureSnapshot,
            PrivilegedOperation.VerifyOptimization,
        };

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
            IpcConstants.PipeName,
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

        string? clientIdentity = null;
        try
        {
            clientIdentity = pipe.GetImpersonationUserName();
            if (string.IsNullOrWhiteSpace(clientIdentity))
            {
                logger.LogWarning("Solicitud IPC sin identidad; rechazada.");
                await WriteResponse(pipe, PrivilegedOperationResponse.Rejected("Identidad IPC no disponible."), stoppingToken);
                return;
            }

            var request = await JsonSerializer.DeserializeAsync<PrivilegedOperationRequest>(pipe, cancellationToken: timeout.Token);
            var response = await ValidateAndDispatchAsync(request, timeout.Token);
            logger.LogInformation(
                "Auditoría IPC: identidad={Identity} operación={Operation} aceptado={Accepted} error={Error}",
                clientIdentity, request?.Operation.ToString() ?? "desconocida", response.Accepted, response.Error ?? "-");
            await WriteResponse(pipe, response, stoppingToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("Solicitud IPC cancelada o excedió el tiempo límite.");
        }
        catch (JsonException)
        {
            await WriteResponse(pipe, PrivilegedOperationResponse.Rejected("JSON inválido."), stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo inesperado atendiendo la conexión IPC de {Client}.", clientIdentity ?? "?");
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

        if (!AllowedOperations.Contains(request.Operation))
        {
            return PrivilegedOperationResponse.Rejected("Operación no permitida por la lista blanca del servicio.");
        }

        // Replay protection: each request id and nonce may be used exactly once.
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
                PrivilegedOperation.CaptureSnapshot => Describe(engine.CaptureSnapshot(request.Parameters.OptimizationId!)),
                PrivilegedOperation.VerifyOptimization => await VerifyAsync(request.Parameters.OptimizationId!, ct),
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

    private async Task<PrivilegedOperationResponse> VerifyAsync(string optimizationId, CancellationToken ct)
    {
        var verification = await engine.VerifyAsync(optimizationId, ct);
        return new PrivilegedOperationResponse(
            verification.Verified,
            verification.Verified ? null : verification.MessageEs,
            $"Verificación: {verification.MessageEs} (estado observado: {verification.ObservedState})");
    }

    private static PrivilegedOperationResponse FromResult(CAO.Core.Abstractions.OperationResult result) =>
        new(result.Success, result.Error, result.MessageEs);

    private static PrivilegedOperationResponse Describe(SnapshotDescriptor descriptor) =>
        new(true, null, $"Snapshot capturado: {descriptor.SnapshotId} ({descriptor.EntryCount} entradas).");

    private static async Task WriteResponse(
        NamedPipeServerStream pipe,
        PrivilegedOperationResponse response,
        CancellationToken ct)
    {
        try
        {
            await JsonSerializer.SerializeAsync(pipe, response, cancellationToken: ct);
        }
        catch (IOException)
        {
            // Client disconnected before reading the answer; nothing to do.
        }
    }
}
