namespace CAO.Shared;

/// <summary>
/// Correlation IDs para conectar logs UI/Core/Infrastructure/Privileged (§159).
/// Cada operación crítica genera TransactionId/RequestId/CorrelationId enlazados.
/// </summary>
public static class Correlation
{
    public static string New() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    public static string From(Guid id) => id.ToString("N")[..12].ToUpperInvariant();
}
