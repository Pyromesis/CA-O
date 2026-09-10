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
    private static readonly TimeSpan DispatchTimeoutDefault = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DispatchTimeoutHeavy = TimeSpan.FromMinutes(20);
    private static readonly HashSet<string> HeavyOptimizationIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows-component-store-cleanup",
        "windows-component-store-resetbase",
        "optimize-system-drive",
        "optimize-hdd-media-aware",
        "retrim-system-ssd",
        "disk-cleanup-system-files",
        "reset-network-stack-repair",
        "repair-windows-update",
    };
    private readonly Core.Security.IIpcReplayGuard _replayGuard = new Core.Security.ReplayCache();


    /// <summary>Máximo de despachos simultáneos; las conexiones siempre se aceptan.</summary>
    private const int MaxConcurrentDispatch = 4;
    private readonly SemaphoreSlim _gate = new(MaxConcurrentDispatch, MaxConcurrentDispatch);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Multi-instancia: por cada conexión aceptada se crea la siguiente
        // ANTES de atenderla, y cada cliente se atiende en su propia tarea.
        // Antes había una sola instancia secuencial: mientras se procesaba
        // una operación (p. ej. reiniciar explorer, crear restore point),
        // nadie más podía ni CONECTAR y la UI fallaba con CAO-IPC-007/004.
        var pending = CreateServer();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pending.WaitForConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                pending.Dispose();
                break;
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "La conexión IPC terminó de forma inesperada.");
                try { pending.Dispose(); } catch { }
                pending = CreateServer();
                continue;
            }
            var accepted = pending;
            try
            {
                pending = CreateServer();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo crear la siguiente instancia del pipe.");
                _ = HandleOneAsync(accepted, stoppingToken);
                try { await Task.Delay(500, stoppingToken); }
                catch (OperationCanceledException) { accepted.Dispose(); break; }
                pending = CreateServer();
                continue;
            }
            _ = HandleOneAsync(accepted, stoppingToken);
        }
    }

    private async Task HandleOneAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        await using (pipe)
        {
            var acquired = false;
            try
            {
                await _gate.WaitAsync(stoppingToken);
                acquired = true;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            try
            {
                await HandleClientAsync(pipe, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo inesperado atendiendo un cliente IPC concurrente.");
            }
            finally
            {
                if (acquired) _gate.Release();
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
            NamedPipeServerStream.MaxAllowedServerInstances,
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
            // Timeout de despacho según la operación: las pesadas (DISM/defrag)
            // necesitan minutos; el techo de 15 s solo aplica a la lectura.
            var dispatchTimeout = IsHeavy(request) ? DispatchTimeoutHeavy : DispatchTimeoutDefault;
            using var dispatchCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            dispatchCts.CancelAfter(dispatchTimeout);
            // Suplantar al llamante: HKCU y carpetas de perfil (%TEMP%, etc.)
            // deben resolverse en SU hive, no en el de SYSTEM. Sin suplantación,
            // lo aplicado a HKCU sería invisible para la UI (siempre "no aplicado").
            var response = await DispatchAsCallerAsync(pipe, () => ValidateAndDispatchAsync(request, caller, dispatchCts.Token));

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

    private static bool IsHeavy(IpcRequest? request) =>
        (request?.Payload is IOptimizationIdPayload p && HeavyOptimizationIds.Contains(p.OptimizationId)) ||
        request?.Operation is PrivilegedOperationKind.SearchDriverUpdates
            or PrivilegedOperationKind.InstallDriverUpdates
            or PrivilegedOperationKind.RemovePhantomDevices;

    /// <summary>
    /// Ejecuta el despacho suplantando al llamante autorizado para que HKCU y
    /// las rutas de perfil se resuelvan en su hive (no en el de SYSTEM).
    /// No eleva privilegios: el llamante ya está autorizado como administrador
    /// elevado; solo reduce el contexto de SYSTEM al del usuario. Si la
    /// suplantación falla, se ejecuta como SYSTEM (comportamiento anterior).
    /// </summary>
    private async Task<IpcResponse> DispatchAsCallerAsync(
        NamedPipeServerStream pipe,
        Func<Task<IpcResponse>> dispatch)
    {
        WindowsIdentity? callerIdentity = null;
        try
        {
            pipe.RunAsClient(() => { callerIdentity = WindowsIdentity.GetCurrent(); });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo capturar el token del llamante; se ejecuta como SYSTEM.");
        }
        if (callerIdentity is null)
        {
            return await dispatch();
        }
        using (callerIdentity)
        {
            try
            {
                return await WindowsIdentity.RunImpersonated(callerIdentity.AccessToken, dispatch);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Suplantación falló; reintento como SYSTEM.");
                return await dispatch();
            }
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

            if (request.Operation == PrivilegedOperationKind.FixDriver && request.Payload is DriverFixPayload fix)
            {
                try
                {
                    var result = await engine.FixDriverAsync(fix.InstanceId, fix.Action, ct);
                    return result.Success ? IpcResponse.Ok() : IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, result.MessageEs);
                }
                catch (Exception ex)
                {
                    return IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, $"Error corrigiendo driver: {ex.Message}");
                }
            }

            if (request.Operation == PrivilegedOperationKind.InstallDriver && request.Payload is InstallDriverPayload install)
            {
                try
                {
                    var result = await engine.InstallDriverInfAsync(install.InfPath, install.InstanceId, ct);
                    return result.Success ? IpcResponse.Ok() : IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, result.MessageEs);
                }
                catch (Exception ex)
                {
                    return IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, $"Error instalando driver: {ex.Message}");
                }
            }

            if (request.Operation == PrivilegedOperationKind.RemovePhantomDevices && request.Payload is RemovePhantomDevicesPayload phantoms)
            {
                try
                {
                    var result = await engine.RemovePhantomDevicesAsync(phantoms.InstanceIds, ct);
                    return result.Success ? IpcResponse.Ok(result.MessageEs) : IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, result.MessageEs);
                }
                catch (Exception ex)
                {
                    return IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, $"Error limpiando fantasmas: {ex.Message}");
                }
            }

            if (request.Operation == PrivilegedOperationKind.SearchDriverUpdates && request.Payload is SearchDriverUpdatesPayload)
            {
                try
                {
                    var search = await new WindowsUpdateDriverService().SearchAsync(ct);
                    if (!search.Success) return IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, search.MessageEs);
                    return IpcResponse.Ok(JsonSerializer.Serialize(search.Updates, JsonOptions));
                }
                catch (Exception ex)
                {
                    return IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, $"Error buscando drivers: {ex.Message}");
                }
            }

            if (request.Operation == PrivilegedOperationKind.InstallDriverUpdates && request.Payload is InstallDriverUpdatesPayload updates)
            {
                try
                {
                    var installed = await new WindowsUpdateDriverService().InstallAsync(updates.UpdateIds, ct);
                    return installed.Success
                        ? IpcResponse.Ok(installed.MessageEs)
                        : IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, installed.MessageEs);
                }
                catch (Exception ex)
                {
                    return IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, $"Error instalando drivers: {ex.Message}");
                }
            }

            if (request.Operation == PrivilegedOperationKind.SetTimerResolution && request.Payload is SetTimerResolutionPayload timer)
            {
                if (!TimerResolution.TrySet(timer.Resolution100Ns, out var actual, out var timerError))
                {
                    return IpcResponse.Rejected(ErrorCodes.TxnApplyFailed, timerError);
                }
                logger.LogInformation("Timer resolution ajustado a {Actual} (pedido {Pedido}).", actual, timer.Resolution100Ns);
                return IpcResponse.Ok(JsonSerializer.Serialize(new
                {
                    requested = timer.Resolution100Ns,
                    applied = actual,
                    appliedMs = TimerResolution.FormatMs(actual),
                }, JsonOptions));
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
