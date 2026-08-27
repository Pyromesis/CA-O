using CAO.Shared;

namespace CAO.UI;

/// <summary>
/// Traduce Exception → UserFacingError (§109-110) con código, severidad y acción de recuperación.
/// La UI nunca maneja excepciones técnicas crudas.
/// </summary>
public sealed record UserFacingError(
    string Code,
    string UserMessageEs,
    string TechnicalMessage,
    string RecoveryActionEs,
    string? CorrelationId);

public static class ErrorTranslator
{
    public static UserFacingError Translate(Exception ex, string? correlationId = null) => ex switch
    {
        UnauthorizedAccessException => new(ErrorCodes.SecStandardUserDenied, "Acceso denegado: se requieren privilegios de administrador.", ex.Message, "Ejecute CA-O Service como administrador o use modo solo lectura.", correlationId),
        OperationCanceledException => new("CAO-CANCELLED", "Operación cancelada.", ex.Message, "Reintente si lo desea.", correlationId),
        TimeoutException => new(ErrorCodes.IpcRequestExpired, "Tiempo de espera agotado.", ex.Message, "Reintente; verifique que el servicio responda.", correlationId),
        System.IO.IOException io => new(ErrorCodes.UiServiceUnavailable, "No se pudo conectar con el servicio privilegiado.", io.Message, "Verifique que CA-O Service esté instalado e iniciado.", correlationId),
        _ => new(ErrorCodes.UiDiagnosticsFailed, "Ocurrió un error inesperado.", ex.GetType().Name + ": " + ex.Message, "Reintente; si persiste, genere un bundle de soporte.", correlationId),
    };

    public static UserFacingError FromResponse(string? errorCode, string? safeMessage, string? correlationId = null) =>
        new(errorCode ?? ErrorCodes.UiServiceUnavailable, safeMessage ?? "Operación rechazada.", safeMessage ?? "", "Revise el código y la descripción.", correlationId);
}
