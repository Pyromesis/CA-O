using System.Text.Json.Serialization;

namespace CAO.Shared.IPC;

/// <summary>Wire protocol version (FASE 3). Bumped on any breaking change.</summary>
public static class IpcProtocol
{
    public const int Version = 2;

    /// <summary>Hard cap for a single request envelope (bytes).</summary>
    public const int MaxRequestBytes = 64 * 1024;

    /// <summary>Hard cap for a single response envelope (bytes).</summary>
    public const int MaxResponseBytes = 256 * 1024;

    /// <summary>Requests older than this are rejected as expired.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);
}

/// <summary>The five privileged operations; each carries exactly one typed payload.</summary>
public enum PrivilegedOperationKind
{
    ApplyOptimization,
    RevertOptimization,
    VerifyOptimization,
    DetectOptimization,
    CaptureSnapshot,
}

/// <summary>
/// Marker for operation-specific payloads (FASE 3). Each request type
/// carries only its own parameters — no grab-bag parameter bags.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$payload")]
[JsonDerivedType(typeof(OptimizationTargetPayload), "target")]
public interface ITypedPayload
{
}

/// <summary>Targeted payload shared by the five catalog operations.</summary>
public sealed record OptimizationTargetPayload(string OptimizationId) : ITypedPayload;

/// <summary>
/// Versioned request envelope. Every field is validated by the service
/// before dispatch; unknown payloads are rejected by schema.
/// </summary>
public sealed record IpcRequest(
    int ProtocolVersion,
    Guid RequestId,
    string Nonce,
    DateTime CreatedAtUtc,
    PrivilegedOperationKind Operation,
    ITypedPayload Payload);

/// <summary>
/// Structured response with machine-readable error codes (FASE 28):
/// CAO-SEC-nnn / CAO-IPC-nnn / CAO-TXN-nnn / CAO-VERIFY-nnn / CAO-ROLLBACK-nnn.
/// </summary>
public sealed record IpcResponse(
    bool Accepted,
    string? ErrorCode,
    string? SafeMessage,
    string? DetailJson)
{
    public static IpcResponse Ok(string? detailJson = null) => new(true, null, null, detailJson);

    public static IpcResponse Rejected(string errorCode, string safeMessage) =>
        new(false, errorCode, safeMessage, null);
}
