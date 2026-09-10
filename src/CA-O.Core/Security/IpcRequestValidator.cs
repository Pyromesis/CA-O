using System.Text.RegularExpressions;
using CAO.Shared;
using CAO.Shared.IPC;

namespace CAO.Core.Security;


/// <summary>
/// Validates a decoded IPC request against every protocol rule (FASE 3):
/// version, identity fields, freshness, size and payload schema. Pure and
/// unit-testable; the pipe host applies it before any dispatch.
/// </summary>
public static class IpcRequestValidator
{
    public static bool TryValidate(IpcRequest? request, out string errorCode, out string error)
    {
        errorCode = ErrorCodes.IpcMalformedRequest;
        error = "Solicitud inválida.";

        if (request is null)
        {
            return false;
        }

        if (request.ProtocolVersion != IpcProtocol.Version)
        {
            errorCode = ErrorCodes.IpcProtocolVersionMismatch;
            error = $"Versión de protocolo {request.ProtocolVersion} no admitida (esperada {IpcProtocol.Version}).";
            return false;
        }

        if (request.RequestId == Guid.Empty)
        {
            error = "RequestId vacío.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) ||
            request.Nonce.Length > 128 ||
            request.Nonce.Any(char.IsControl))
        {
            error = "Nonce ausente o con formato inválido.";
            return false;
        }

        if (DateTime.UtcNow - request.CreatedAtUtc > IpcProtocol.MaxAge || request.CreatedAtUtc > DateTime.UtcNow.AddMinutes(1))
        {
            errorCode = ErrorCodes.IpcRequestExpired;
            error = "Solicitud expirada o con reloj futuro.";
            return false;
        }

        if (!Enum.IsDefined(request.Operation))
        {
            error = "Operación no definida.";
            return false;
        }

        // Ping, GetServiceStatus, SetDns, SetTimerResolution, FixDriver,
        // InstallDriver, RemovePhantomDevices, SearchDriverUpdates e
        // InstallDriverUpdates no llevan OptimizationId (§10, FASE 8)
        if (request.Operation is PrivilegedOperationKind.Ping or PrivilegedOperationKind.GetServiceStatus)
        {
            var pingExpected = request.Operation == PrivilegedOperationKind.Ping ? typeof(global::CAO.Shared.IPC.PingPayload) : typeof(global::CAO.Shared.IPC.GetServiceStatusPayload);
            if (request.Payload.GetType() != pingExpected)
            {
                errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
                error = "Payload de Ping/Status inválido.";
                return false;
            }
            error = string.Empty;
            return true;
        }
        if (request.Operation is PrivilegedOperationKind.SetDns)
        {
            if (request.Payload is not global::CAO.Shared.IPC.SetDnsPayload dns)
            {
                errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
                error = "Payload de SetDns inválido.";
                return false;
            }
            var dnsParts = (dns.DnsIp ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (string.IsNullOrWhiteSpace(dns.InterfaceName) || dnsParts.Length == 0 || dnsParts.Length > 2 ||
                !dnsParts.All(p => System.Net.IPAddress.TryParse(p, out _)))
            {
                error = "SetDns InterfaceName/DnsIp no valido.";
                return false;
            }
            error = string.Empty;
            return true;
        }
        if (request.Operation is PrivilegedOperationKind.SetTimerResolution)
        {
            if (request.Payload is not global::CAO.Shared.IPC.SetTimerResolutionPayload timer)
            {
                errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
                error = "Payload de SetTimerResolution inválido.";
                return false;
            }
            if (timer.Resolution100Ns is < 1000 or > 156250)
            {
                error = "SetTimerResolution fuera de rango (1000..156250).";
                return false;
            }
            error = string.Empty;
            return true;
        }

        if (request.Operation is PrivilegedOperationKind.FixDriver)
        {
            if (request.Payload is not global::CAO.Shared.IPC.DriverFixPayload fix)
            {
                errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
                error = "Payload de FixDriver inválido.";
                return false;
            }
            if (!global::CAO.Shared.IPC.FixDriverActions.IsValid(fix.Action) ||
                !global::CAO.Shared.Security.CommandPolicy.IsValidPnpInstanceId(fix.InstanceId))
            {
                error = "FixDriver InstanceId/Action no valido.";
                return false;
            }
            error = string.Empty;
            return true;
        }
        if (request.Operation is PrivilegedOperationKind.InstallDriver)
        {
            if (request.Payload is not global::CAO.Shared.IPC.InstallDriverPayload install)
            {
                errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
                error = "Payload de InstallDriver inválido.";
                return false;
            }
            if (!global::CAO.Shared.Security.CommandPolicy.IsValidInfPath(install.InfPath) ||
                (!string.IsNullOrEmpty(install.InstanceId) &&
                 !global::CAO.Shared.Security.CommandPolicy.IsValidPnpInstanceId(install.InstanceId)))
            {
                error = "InstallDriver InfPath/InstanceId no valido.";
                return false;
            }
            error = string.Empty;
            return true;
        }
        if (request.Operation is PrivilegedOperationKind.RemovePhantomDevices)
        {
            if (request.Payload is not global::CAO.Shared.IPC.RemovePhantomDevicesPayload phantoms)
            {
                errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
                error = "Payload de RemovePhantomDevices inválido.";
                return false;
            }
            if (phantoms.InstanceIds is null || phantoms.InstanceIds.Count == 0 || phantoms.InstanceIds.Count > 200 ||
                !phantoms.InstanceIds.All(global::CAO.Shared.Security.CommandPolicy.IsValidPnpInstanceId))
            {
                error = "RemovePhantomDevices: lista vacía, excesiva o con IDs no válidos.";
                return false;
            }
            error = string.Empty;
            return true;
        }
        if (request.Operation is PrivilegedOperationKind.SearchDriverUpdates)
        {
            if (request.Payload is not global::CAO.Shared.IPC.SearchDriverUpdatesPayload)
            {
                errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
                error = "Payload de SearchDriverUpdates inválido.";
                return false;
            }
            error = string.Empty;
            return true;
        }
        if (request.Operation is PrivilegedOperationKind.InstallDriverUpdates)
        {
            if (request.Payload is not global::CAO.Shared.IPC.InstallDriverUpdatesPayload updates)
            {
                errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
                error = "Payload de InstallDriverUpdates inválido.";
                return false;
            }
            if (updates.UpdateIds is null || updates.UpdateIds.Count == 0 || updates.UpdateIds.Count > 50 ||
                !updates.UpdateIds.All(global::CAO.Shared.Security.CommandPolicy.IsValidWindowsUpdateId))
            {
                error = "InstallDriverUpdates: lista vacía, excesiva o con IDs no válidos.";
                return false;
            }
            error = string.Empty;
            return true;
        }
        if (request.Payload is not global::CAO.Shared.IPC.IOptimizationIdPayload target)
        {
            errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
            error = "Payload no coincide con el esquema de la operacion.";
            return false;
        }

        // P1-12: Operation <-> PayloadType must agree exactly.
        var expectedType = request.Operation switch
        {
            PrivilegedOperationKind.ApplyOptimization => typeof(global::CAO.Shared.IPC.ApplyOptimizationPayload),
            PrivilegedOperationKind.RevertOptimization => typeof(global::CAO.Shared.IPC.RevertOptimizationPayload),
            PrivilegedOperationKind.DetectOptimization => typeof(global::CAO.Shared.IPC.DetectOptimizationPayload),
            PrivilegedOperationKind.VerifyOptimization => typeof(global::CAO.Shared.IPC.VerifyOptimizationPayload),
            PrivilegedOperationKind.CaptureSnapshot => typeof(global::CAO.Shared.IPC.CaptureSnapshotPayload),
            _ => null,
        };

        if (expectedType is null || request.Payload.GetType() != expectedType)
        {
            errorCode = ErrorCodes.IpcPayloadSchemaInvalid;
            error = "El tipo de payload no corresponde a la operacion solicitada.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.OptimizationId) ||
            !Regex.IsMatch(target.OptimizationId, "^[a-z0-9-]{1,80}$"))
        {
            error = "OptimizationId no valido.";
            return false;
        }


        error = string.Empty;
        return true;
    }
}

/// <summary>Replay guard: each RequestId and nonce is accepted exactly once.</summary>
public interface IIpcReplayGuard
{
    bool TryAccept(Guid requestId, string nonce);
}
