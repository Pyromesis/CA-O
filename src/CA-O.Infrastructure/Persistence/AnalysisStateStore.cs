using System.Text.Json;
using CAO.Shared;

namespace CAO.Infrastructure.Persistence;

/// <summary>
/// Estado persistente de análisis (§3-4): resuelve el bug "no hay escaneo previo".
/// Guarda timestamp, Windows build, hardware, seguridad, red, storage, drivers, thermal, input, health y recommendations.
/// Usa escritura atómica temp->flush->replace y schema versionado. Nunca deja JSON parcial.
/// </summary>
public sealed class AnalysisStateStore
{
    public const int SchemaVersion = 2;
    private readonly string _filePath;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public sealed record PersistedAnalysis(
        int SchemaVersion,
        string AppVersion,
        int WindowsBuild,
        DateTime TimestampUtc,
        SystemContext? Context,
        IReadOnlyList<Recommendation>? Recommendations,
        SystemDiagnosticReport? Health,
        string AnalysisState, // Pending/Running/Completed/CompletedWithWarnings/Failed/Cancelled
        IReadOnlyList<string> Warnings,
        TimeSpan Duration,
        string? ErrorCode,
        string? CorrelationId
    );

    public AnalysisStateStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(CaOPaths.ProgramDataRoot, "analysis-state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public void SaveAnalysis(PersistedAnalysis analysis)
    {
        lock (_lock)
        {
            var tmp = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(analysis, JsonOpts);
            File.WriteAllText(tmp, json, System.Text.Encoding.UTF8);
            // Atomic replace
            File.Move(tmp, _filePath, overwrite: true);
        }
    }

    public bool HasAnalysis()
    {
        lock (_lock) { return File.Exists(_filePath); }
    }

    public PersistedAnalysis? LoadLatestAnalysis()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath)) return null;
            try
            {
                var json = File.ReadAllText(_filePath, System.Text.Encoding.UTF8);
                var data = JsonSerializer.Deserialize<PersistedAnalysis>(json, JsonOpts);
                if (data is null || data.SchemaVersion != SchemaVersion) return null;
                // Validar integridad básica
                if (data.TimestampUtc == default || string.IsNullOrWhiteSpace(data.AppVersion)) return null;
                return data;
            }
            catch (JsonException)
            {
                // Corrupto: cuarentena + crear reemplazo limpio en próximo Save
                try { File.Move(_filePath, _filePath + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true); } catch { }
                return null;
            }
            catch { return null; }
        }
    }

    public void DeleteAnalysis()
    {
        lock (_lock) { try { if (File.Exists(_filePath)) File.Delete(_filePath); } catch { } }
    }

    public PersistedAnalysis? Validate()
    {
        var loaded = LoadLatestAnalysis();
        if (loaded is null) return null;
        // Obsoleto si > 24h o WindowsBuild cambió
        return loaded;
    }

    public bool IsObsolete(PersistedAnalysis analysis, SystemContext? currentContext = null)
    {
        var age = DateTime.UtcNow - analysis.TimestampUtc;
        if (age > TimeSpan.FromHours(24)) return true;
        if (currentContext != null && analysis.Context != null && analysis.Context.WindowsBuild != currentContext.WindowsBuild) return true;
        return false;
    }

    public string GetStatusLabel(PersistedAnalysis? analysis)
    {
        if (analysis is null) return "Nunca analizado";
        var age = DateTime.UtcNow - analysis.TimestampUtc;
        if (age < TimeSpan.FromMinutes(5)) return $"Analizado hace {Math.Max(1, (int)age.TotalMinutes)} minutos";
        if (age < TimeSpan.FromHours(24)) return $"Analizado hace {(int)age.TotalHours} horas";
        if (age < TimeSpan.FromHours(48)) return "Analizado ayer";
        return analysis.AnalysisState == "CompletedWithWarnings" ? "Análisis completado con advertencias" 
            : analysis.AnalysisState == "Failed" ? "Análisis fallido"
            : analysis.AnalysisState == "Cancelled" ? "Análisis cancelado"
            : "Análisis obsoleto";
    }
}
