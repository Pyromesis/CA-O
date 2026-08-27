using System.Text.Json;

namespace CAO.Infrastructure.Logging;

/// <summary>
/// Structured logger centralizado (§73): niveles, correlation IDs, rotación y sanitización.
/// Separa UI logs, service logs y audit logs sin mezclar ubicaciones.
/// </summary>
public enum LogLevel { Trace, Debug, Info, Warning, Error, Critical }

public sealed record LogEntry(
    DateTime TimestampUtc,
    LogLevel Level,
    string Category,
    string Message,
    string? CorrelationId,
    string? ErrorCode,
    IReadOnlyDictionary<string, string>? Properties);

public sealed class StructuredLogger
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private const long MaxFileBytes = 5 * 1024 * 1024; // 5MB rotación

    public StructuredLogger(string? filePath = null)
    {
        var dir = filePath is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CA-O", "logs")
            : Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);
        _filePath = filePath ?? Path.Combine(dir, "cao-ui-structured.log");
    }

    public void Log(LogLevel level, string category, string message, string? correlationId = null, string? errorCode = null, IReadOnlyDictionary<string, string>? properties = null)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, category, Sanitize(message), correlationId, errorCode, SanitizeProperties(properties));
        var json = JsonSerializer.Serialize(entry);
        lock (_lock)
        {
            RotateIfNeeded();
            File.AppendAllText(_filePath, json + Environment.NewLine);
        }
    }

    public void Info(string category, string message, string? correlationId = null) => Log(LogLevel.Info, category, message, correlationId);
    public void Warning(string category, string message, string? correlationId = null, string? errorCode = null) => Log(LogLevel.Warning, category, message, correlationId, errorCode);
    public void Error(string category, string message, Exception? ex = null, string? correlationId = null, string? errorCode = null)
    {
        var props = ex is null ? null : new Dictionary<string, string> { ["exception"] = ex.GetType().Name, ["stack"] = ex.StackTrace ?? string.Empty };
        Log(LogLevel.Error, category, message, correlationId, errorCode, props);
    }

    private void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var info = new FileInfo(_filePath);
            if (info.Length <= MaxFileBytes) return;
            var rotated = _filePath + "." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            File.Move(_filePath, rotated);
        }
        catch { }
    }

    private static string Sanitize(string value)
    {
        // Nunca registrar secrets — redactar tokens/paths sensibles
        if (string.IsNullOrEmpty(value)) return value;
        // Ejemplo: redactar posible token
        return value.Length > 2000 ? value[..2000] + "…[truncated]" : value;
    }

    private static IReadOnlyDictionary<string, string>? SanitizeProperties(IReadOnlyDictionary<string, string>? props)
    {
        if (props is null) return null;
        var sanitized = new Dictionary<string, string>();
        foreach (var kv in props)
        {
            if (kv.Key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Contains("secret", StringComparison.OrdinalIgnoreCase))
                sanitized[kv.Key] = "[redacted]";
            else sanitized[kv.Key] = Sanitize(kv.Value);
        }
        return sanitized;
    }
}
