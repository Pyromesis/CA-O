namespace CAO.Shared;

/// <summary>
/// Fuente única de verdad para el estado del servicio privilegiado en la UI.
/// Un pipe inalcanzable (servicio detenido/no instalado) devuelve rejection
/// CAO-IPC-007/008: eso es "unavailable", nunca "rejected".
/// </summary>
public static class ServiceStatusMapper
{
    /// <summary>
    /// Mapea una respuesta de ping al estado canónico:
    /// connected / unavailable / rejected.
    /// </summary>
    public static string FromPing(bool accepted, string? errorCode) =>
        accepted ? "connected"
        : errorCode is ErrorCodes.IpcPipeNotFound or ErrorCodes.IpcTimeout ? "unavailable"
        : "rejected";

    /// <summary>¿Es un estado de servicio caído (reintentable)?</summary>
    public static bool IsUnavailable(string? status) =>
        status is "unavailable" or "no disponible";

    /// <summary>¿Merece verificación (AutoCheck)? connected/rejected no.</summary>
    public static bool NeedsVerification(string? status) =>
        status is null or "unknown" or "unavailable" or "no disponible";

    /// <summary>
    /// Throttle del AutoCheck: 60 s para unavailable (el servicio puede volver
    /// tras una limpieza o un arranque lento), 5 min para el resto.
    /// </summary>
    public static TimeSpan AutoCheckThrottle(string? status) =>
        IsUnavailable(status) ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5);
}
