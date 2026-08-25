using System.Text.RegularExpressions;

namespace CAO.Shared;

public enum PrivilegedOperation
{
    ApplyOptimization,
    RevertOptimization,
    CaptureSnapshot,
    VerifyOptimization,
    DetectOptimization,
}

public sealed record OperationParameters
{
    public string? OptimizationId { get; init; }

    public char? DriveLetter { get; init; }

    public string? ServiceName { get; init; }
}

public sealed record PrivilegedOperationRequest
{
    public int ProtocolVersion { get; init; } = AppVersion.ProtocolVersion;

    public required Guid RequestId { get; init; }

    public required string Nonce { get; init; }

    public required PrivilegedOperation Operation { get; init; }

    public required OperationParameters Parameters { get; init; }
}

/// <summary>Wire response shared by the privileged service and every client.</summary>
public sealed record PrivilegedOperationResponse(bool Accepted, string? Error, string? MessageEs = null)
{
    public static PrivilegedOperationResponse Rejected(string error) => new(false, error);
}

public static class PrivilegedOperationValidator
{
    private static readonly Regex SafeIdentifier = new("^[a-z0-9-]{1,80}$", RegexOptions.CultureInvariant);

    public static bool TryValidate(PrivilegedOperationRequest request, out string error)
    {
        if (request.ProtocolVersion != AppVersion.ProtocolVersion)
        {
            error = "Versión de protocolo no admitida.";
            return false;
        }

        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Nonce) || request.Nonce.Length > 128 || request.Nonce.Any(char.IsControl))
        {
            error = "La solicitud no tiene identidad válida.";
            return false;
        }

        if (!Enum.IsDefined(request.Operation))
        {
            error = "Operación no permitida.";
            return false;
        }

        var parameters = request.Parameters;
        if (parameters is null)
        {
            error = "Faltan parámetros tipados.";
            return false;
        }

        if (parameters.OptimizationId is not null && !SafeIdentifier.IsMatch(parameters.OptimizationId))
        {
            error = "OptimizationId no válido.";
            return false;
        }

        if (parameters.ServiceName is not null && !Regex.IsMatch(parameters.ServiceName, "^[A-Za-z0-9_-]{1,256}$", RegexOptions.CultureInvariant))
        {
            error = "Nombre de servicio no válido.";
            return false;
        }

        if (parameters.DriveLetter is not null && (parameters.DriveLetter < 'A' || parameters.DriveLetter > 'Z'))
        {
            error = "Unidad no válida.";
            return false;
        }

        var requiresOptimizationId = request.Operation is
            PrivilegedOperation.ApplyOptimization or
            PrivilegedOperation.RevertOptimization or
            PrivilegedOperation.DetectOptimization or
            PrivilegedOperation.CaptureSnapshot or
            PrivilegedOperation.VerifyOptimization;

        if (requiresOptimizationId && string.IsNullOrWhiteSpace(parameters.OptimizationId))
        {
            error = "La operación requiere OptimizationId.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}