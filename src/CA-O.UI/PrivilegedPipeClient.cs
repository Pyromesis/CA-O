using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using CAO.Shared;
using CAO.Shared.IPC;
using Microsoft.Win32.SafeHandles;

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

    // Techo de lectura de RESPUESTA por operación (el servicio despacha 60 s
    // por defecto y 20 min en pesadas): sin techo, un servicio colgado a mitad
    // deja la UI colgada hasta el timeout del llamante (45 min en drivers).
    // La cancelación del llamante sigue respetándose (lo primero que llegue).
    private static readonly TimeSpan ResponseTimeoutDefault = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ResponseTimeoutHeavy = TimeSpan.FromMinutes(21);

    private static readonly HashSet<PrivilegedOperationKind> HeavyOperations = new()
    {
        PrivilegedOperationKind.SearchDriverUpdates,
        PrivilegedOperationKind.InstallDriverUpdates,
        PrivilegedOperationKind.RemovePhantomDevices,
        PrivilegedOperationKind.ExportDriver,
        PrivilegedOperationKind.SearchCatalogDrivers,
        PrivilegedOperationKind.DownloadCatalogDriver,
    };

    // Espejo de PrivilegedPipeService.HeavyOptimizationIds: Apply/Revert de
    // estas optimizaciones despacha hasta 20 min en el servicio.
    private static readonly HashSet<string> HeavyOptimizationIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows-component-store-cleanup",
        "windows-component-store-resetbase",
        "optimize-system-drive",
        "retrim-system-ssd",
        "defragment-hdd-only",
        "disk-cleanup-system-files",
        "reset-network-stack-repair",
        "repair-windows-update",
    };

    private static bool IsHeavyOptimization(ITypedPayload payload) =>
        payload is IOptimizationIdPayload p && HeavyOptimizationIds.Contains(p.OptimizationId);

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

    public Task<IpcResponse?> RemovePhantomDevicesAsync(IReadOnlyList<string> instanceIds, CancellationToken ct = default) =>
        SendPayloadAsync(PrivilegedOperationKind.RemovePhantomDevices, new RemovePhantomDevicesPayload(instanceIds), ct);

    public Task<IpcResponse?> SearchDriverUpdatesAsync(CancellationToken ct = default) =>
        SendPayloadAsync(PrivilegedOperationKind.SearchDriverUpdates, new SearchDriverUpdatesPayload(), ct);

    public Task<IpcResponse?> InstallDriverUpdatesAsync(IReadOnlyList<string> updateIds, CancellationToken ct = default) =>
        SendPayloadAsync(PrivilegedOperationKind.InstallDriverUpdates, new InstallDriverUpdatesPayload(updateIds), ct);

    public Task<IpcResponse?> ExportDriverAsync(string instanceId, CancellationToken ct = default) =>
        SendPayloadAsync(PrivilegedOperationKind.ExportDriver, new ExportDriverPayload(instanceId), ct);

    public Task<IpcResponse?> SearchCatalogDriversAsync(string hardwareId, string deviceName, CancellationToken ct = default) =>
        SendPayloadAsync(PrivilegedOperationKind.SearchCatalogDrivers, new SearchCatalogDriversPayload(hardwareId, deviceName), ct);

    public Task<IpcResponse?> DownloadCatalogDriverAsync(string updateId, string hardwareId, CancellationToken ct = default) =>
        SendPayloadAsync(PrivilegedOperationKind.DownloadCatalogDriver, new DownloadCatalogDriverPayload(updateId, hardwareId), ct);

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
            await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcTimeout, "Servicio no disponible: tiempo de espera agotado (CAO-IPC-007). Verifique que CA-O Privileged Service esté instalado e iniciado.");
        }
        catch (IOException)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcPipeNotFound, "Servicio no disponible: pipe no encontrado (CAO-IPC-008). Instale/inicie el servicio privilegiado con scripts/install-privileged-service.ps1.");
        }
        catch (TimeoutException)
        {
            return IpcResponse.Rejected(ErrorCodes.IpcTimeout, "Servicio no disponible: timeout al conectar (CAO-IPC-007).");
        }

        // Anti pipe-squatting: un malware de usuario estándar puede crear el
        // pipe antes que el servicio al arrancar y falsificar respuestas
        // (DoS + espionaje de nombres/IDs). Se exige dueño SYSTEM en sesión 0
        // con binario bajo Program Files\CA-O antes de enviar nada.
        if (!PipeServerTrust.IsTrustedServer(pipe, out var serverDetail))
        {
            try { pipe.Close(); } catch { }
            return IpcResponse.Rejected(ErrorCodes.IpcPipeNotFound, $"Servidor IPC no confiable ({serverDetail}, CAO-IPC-008). Posible pipe suplantado: reinstala el servicio con scripts/install-privileged-service.ps1 elevado.");
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
                await writer.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);
                writer.Flush();
            }
            // Leer una línea de respuesta con el timeout del llamante ACOTADO
            // al techo de la operación (nunca más allá del despacho del
            // servicio + margen): un servicio colgado responde CAO-IPC-007
            // en vez de congelar la UI.
            var responseCeiling = HeavyOperations.Contains(operation) || IsHeavyOptimization(payload)
                ? ResponseTimeoutHeavy
                : ResponseTimeoutDefault;
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            readCts.CancelAfter(responseCeiling);
            string? line;
            using (var reader = new StreamReader(pipe, System.Text.Encoding.UTF8, false, 1024, leaveOpen: true))
            {
                // Lectura acotada a MaxResponseBytes: sin techo, una
                // respuesta gigante (servicio comprometido o salida sin
                // truncar) congela la UI u OOM. Espejo del ReadBoundedLine
                // del servicio.
                line = await ReadBoundedLineAsync(reader, IpcProtocol.MaxResponseBytes + 1024, readCts.Token).ConfigureAwait(false);
            }
            if (line is null)
                return IpcResponse.Rejected(ErrorCodes.IpcMalformedRequest, "Respuesta excede 256KB.");
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

    /// <summary>
    /// Lee una línea con techo duro de caracteres. null si se supera el
    /// máximo (el llamante rechaza en vez de alojar MBs en memoria).
    /// </summary>
    private static async Task<string?> ReadBoundedLineAsync(StreamReader reader, int maxChars, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder(4096);
        var buf = new char[1024];
        var gotData = false;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var n = await reader.ReadAsync(buf.AsMemory(0, buf.Length), ct).ConfigureAwait(false);
            if (n == 0) break;
            gotData = true;
            for (var i = 0; i < n; i++)
            {
                var c = buf[i];
                if (c == '\n') return sb.ToString().TrimEnd('\r');
                sb.Append(c);
                if (sb.Length > maxChars) return null;
            }
            if (sb.Length > maxChars) return null;
        }
        return gotData ? sb.ToString() : string.Empty;
    }
}

/// <summary>
/// Anti pipe-squatting (lado cliente): verifica que el dueño del pipe sea el
/// servicio real antes de enviar la solicitud. Un proceso de usuario puede
/// pre-crear el mismo nombre de pipe al arrancar; sin esta comprobación la
/// UI le entregaría nombres de interfaces/IDs y aceptaría sus respuestas.
/// Criterio: proceso en sesión 0 (solo SYSTEM/servicios) cuyo binario vive
/// bajo Program Files\CA-O. Nunca lanza: a la duda, no confiable.
/// </summary>
internal static class PipeServerTrust
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    public static bool IsTrustedServer(NamedPipeClientStream pipe, out string detail)
    {
        detail = "desconocido";
        try
        {
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var pid) || pid == 0)
            {
                detail = "sin PID de servidor";
                return false;
            }
            using var process = System.Diagnostics.Process.GetProcessById(unchecked((int)pid));
            if (process.SessionId != 0)
            {
                detail = $"sesión {process.SessionId} (esperada 0)";
                return false;
            }
            string? path;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch
            {
                path = null;
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                detail = "binario del servidor no legible";
                return false;
            }
            var full = Path.GetFullPath(path);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var expectedDir = Path.GetFullPath(Path.Combine(programFiles, "CA-O")) + Path.DirectorySeparatorChar;
            string? expectedExe = null;
            try
            {
                expectedExe = CAO.Shared.Constants.BuildConstants.GetServiceExecutablePath();
            }
            catch
            {
                expectedExe = null;
            }
            if (full.StartsWith(expectedDir, StringComparison.OrdinalIgnoreCase) ||
                (expectedExe is not null && full.Equals(Path.GetFullPath(expectedExe), StringComparison.OrdinalIgnoreCase)))
            {
                detail = full;
                return true;
            }
            detail = full;
            return false;
        }
        catch (Exception ex)
        {
            detail = ex.GetType().Name;
            return false;
        }
    }
}
