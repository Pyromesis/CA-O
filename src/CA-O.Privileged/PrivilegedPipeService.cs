using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using CAO.Shared;
using CAO.Shared.IPC;
using CAO.Shared.Security;
using CAO.Core.Engine;
using CAO.Core.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CAO.Privileged;

/// <summary>
/// Privileged pipe host v2 (FASE 2/3): restrictive ACL, real Windows-token
/// authorization after impersonation, versioned typed protocol with replay
/// guard + expiration + size caps, operation allowlist, per-connection
/// timeout and full audit (RequestedBy vs ExecutedBy).
/// </summary>
internal sealed class PrivilegedPipeService(
    ILogger<PrivilegedPipeService> logger,
    OptimizationEngine engine,
    IPrivilegedCallerAuthorizer authorizer) : BackgroundService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly Core.Security.IIpcReplayGuard _replayGuard = new Core.Security.ReplayCache();


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
                logger.LogWarning(ex, "La conexiÃ³n IPC terminÃ³ de forma inesperada.");
            }
        }
    }

    private static NamedPipeServerStream CreateServer()
    {
        var security = new PipeSecurity();
        // SYSTEM: full control. Administrators: read/write. Interactive:
        // read+write so the UI can CONNECT; authorization of the caller's
        // token happens per-request â€” connecting is not authorizing.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            IpcConstants.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            IpcProtocol.MaxRequestBytes,
            IpcProtocol.MaxResponseBytes,
            security);
    }

    /// <summary>
    /// Extracts CallerIdentity from the client token via impersonation
    /// (P1-8): real SID, name, session id and elevation â€” no derived values.
    /// </summary>
    internal static CallerIdentity GetCallerIdentity(
        NamedPipeServerStream pipe,
        CAO.Infrastructure.Windows.Security.WindowsCallerInspector inspector)
    {
        CallerIdentity? captured = null;
        pipe.RunAsClient(() =>
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            captured = inspector.Inspect(identity);
        });
        return captured ?? new CallerIdentity("S-0-0", "?", false, false, -1);
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            // Leer primero para permitir RunAsClient (requiere datos leÃ­dos, ERROR 536)
            string? line;
            using (var reader = new StreamReader(pipe, System.Text.Encoding.UTF8, false, 1024, leaveOpen: true))
            {
                var readTask = reader.ReadLineAsync(timeout.Token).AsTask();
                var completed = await Task.WhenAny(readTask, Task.Delay(RequestTimeout, timeout.Token));
                if (completed != readTask)
                    throw new OperationCanceledException("Timeout leyendo request");
                line = await readTask;
            }
if (string.IsNullOrWhiteSpace(line))
            {
                logger.LogWarning("Empty line received, pipe may have been closed");
                throw new JsonException("Request vacía");
            }
            if (System.Text.Encoding.UTF8.GetByteCount(line) > IpcProtocol.MaxRequestBytes)
            {
                await WriteResponse(pipe, IpcResponse.Rejected(ErrorCodes.IpcRequestTooLarge, "Solicitud excede 64KB."), stoppingToken);
                return;
            }
            // Ahora sÃ­ se puede suplantar: el cliente ya escribiÃ³ y el servidor ya leyÃ³
            CallerIdentity caller;
try { caller = GetCallerIdentity(pipe, new CAO.Infrastructure.Windows.Security.WindowsCallerInspector()); }
            catch (Exception ex)
            {
                logger.LogError(ex, "GetCallerIdentity FAIL");
                throw;
            }
            logger.LogInformation("Conexión IPC de {Sid} ({Name}).", caller.Sid, caller.Name);
            logger.LogDebug("Connected {Sid} {Name} Elevated={IsElevated} Admin={IsAdministrator}", caller.Sid, caller.Name, caller.IsElevated, caller.IsAdministrator);

            IpcRequest? request;
try { request = JsonSerializer.Deserialize<IpcRequest>(line, JsonOptions); }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "JSON inválido recibido: {Line}", line.Length > 200 ? line[..200] : line);
                await WriteResponse(pipe, IpcResponse.Rejected(ErrorCodes.IpcMalformedRequest, $"JSON inválido: {ex.Message}"), stoppingToken);
                return;
            }
            logger.LogDebug("Request OK op={Operation} id={RequestId}", request?.Operation, request?.RequestId);
            var response = await ValidateAndDispatchAsync(request, caller, timeout.Token);

            logger.LogInformation(
                "AuditorÃ­a IPC: requestedBy={Sid}/{Name} executedBy=SYSTEM op={Op} accepted={Accepted} code={Code}",
                caller.Sid, caller.Name, request?.Operation.ToString() ?? "?", response.Accepted, response.ErrorCode ?? "-");

            await WriteResponse(pipe, response, stoppingToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("Solicitud IPC cancelada o excediÃ³ el tiempo lÃ­mite.");
            try { await WriteResponse(pipe, IpcResponse.Rejected(ErrorCodes.IpcTimeout, "Timeout del servicio (CAO-IPC-007)."), stoppingToken); } catch { }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "JSON invÃ¡lido en pipe");
            await WriteResponse(pipe, IpcResponse.Rejected(ErrorCodes.IpcMalformedRequest, $"JSON invÃ¡lido: {ex.Message}"), stoppingToken);
        }
catch (Exception ex)
            {
                logger.LogError(ex, "Fallo inesperado atendiendo la conexión IPC.");
                await WriteResponse(pipe, IpcResponse.Rejected(ErrorCodes.IpcMalformedRequest, $"Error interno: {ex.GetType().Name} — {ex.Message}"), stoppingToken);
            }
    }

    private async Task<IpcResponse> ValidateAndDispatchAsync(IpcRequest? request, CallerIdentity caller, CancellationToken ct)
    {
        if (!IpcRequestValidator.TryValidate(request, out var errorCode, out var error))
        {
            return IpcResponse.Rejected(errorCode, error);
        }

        var authorization = authorizer.Authorize(caller);
        if (!authorization.Allowed)
        {
            return IpcResponse.Rejected(authorization.ReasonCode,
                "El usuario actual no estÃ¡ autorizado para operaciones privilegiadas.");
        }

        // Replay protection: request id and nonce are single-use.
        if (request is null || !_replayGuard.TryAccept(request.RequestId, request.Nonce))
        {
            return IpcResponse.Rejected(ErrorCodes.IpcReplayDetected, "Solicitud repetida.");
        }

        // Ping / GetServiceStatus no requieren OptimizationId (Â§10)
        if (request.Operation is PrivilegedOperationKind.Ping)
        {
            var ping = new PingResponse(CAO.Shared.AppVersion.Semantic, IpcProtocol.Version, Environment.ProcessId, true, "running");
            return IpcResponse.Ok(JsonSerializer.Serialize(ping, JsonOptions));
        }
        if (request.Operation is PrivilegedOperationKind.GetServiceStatus)
        {
            var status = new ServiceStatusResponse(CAO.Shared.AppVersion.Semantic, IpcProtocol.Version, Environment.ProcessId, true, "running", new[] { "ApplyOptimization", "RevertOptimization", "Ping" });
            return IpcResponse.Ok(JsonSerializer.Serialize(status, JsonOptions));
        }

            if (request.Operation == PrivilegedOperationKind.SetDns && request.Payload is SetDnsPayload dns)
            {
                try
                {
                    var result = await engine.SetDnsAsync(dns.InterfaceName, dns.DnsIp, ct);
                    return result.Success ? IpcResponse.Ok() : IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, result.MessageEs);
                }
                catch (Exception ex)
                {
                    return IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, $"Error aplicando DNS: {ex.Message}");
                }
            }

            var optimizationId = ((IOptimizationIdPayload)request.Payload).OptimizationId;

        try
        {
            return request.Operation switch
            {
                PrivilegedOperationKind.ApplyOptimization => FromResult(await engine.ApplyAsync(optimizationId, caller, ct)),
                PrivilegedOperationKind.RevertOptimization => FromResult(await engine.RevertAsync(optimizationId, caller, ct)),
                PrivilegedOperationKind.CaptureSnapshot => Snapshot(engine.CaptureSnapshot(optimizationId)),
                PrivilegedOperationKind.VerifyOptimization => await VerifyAsync(engine.VerifyAsync(optimizationId, ct)),
                PrivilegedOperationKind.DetectOptimization => IpcResponse.Ok($"\"{engine.Detect(optimizationId)}\""),
                _ => IpcResponse.Rejected(ErrorCodes.IpcPayloadSchemaInvalid, "Operación no disponible."),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ejecutando la operaciÃ³n IPC {Operation}.", request.Operation);
            return IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, "La operaciÃ³n fallÃ³ en el sistema.");
        }
    }

    private static async Task<IpcResponse> VerifyAsync(Task<VerificationResult> verificationTask)
    {
        var verification = await verificationTask;
        var detail = JsonSerializer.Serialize(new
        {
            status = verification.Status.ToString(),
            observed = verification.ObservedState.ToString(),
        });
        return verification.Status is VerificationStatus.Passed or VerificationStatus.NotApplicable
            ? IpcResponse.Ok(detail)
            : IpcResponse.Rejected(ErrorCodes.VerifyFailed, $"VerificaciÃ³n: {verification.MessageEs}");
    }

    private static IpcResponse FromResult(CAO.Core.Abstractions.OperationResult result) =>
        result.Success ? IpcResponse.Ok() : IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, result.MessageEs);

    private static IpcResponse Snapshot(SnapshotDescriptor descriptor) =>
        IpcResponse.Ok($"Snapshot capturado: {descriptor.SnapshotId} ({descriptor.EntryCount} entradas).");

    private static async Task WriteResponse(NamedPipeServerStream pipe, IpcResponse response, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(response, JsonOptions);
            using var writer = new StreamWriter(pipe, System.Text.Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(json.AsMemory(), ct);
            writer.Flush();
            pipe.Flush();
        }
        catch (IOException)
        {
            // Client gone before reading the answer.
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
