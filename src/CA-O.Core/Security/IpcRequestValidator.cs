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

        // Ping y GetServiceStatus no llevan OptimizationId (§10)
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
