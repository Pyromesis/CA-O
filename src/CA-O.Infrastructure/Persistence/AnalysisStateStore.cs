using System.Text.Json;
using CAO.Shared;

namespace CAO.Infrastructure.Persistence;

/// <summary>
/// Estado persistente de análisis (§3-4): resuelve el bug "no hay escaneo previo".
/// Guarda timestamp, Windows build, hardware, seguridad, red, storage, drivers, thermal, input, health y recommendations.
/// Usa escritura atómica temp->flush->replace y schema versionado. Nunca deja JSON parcial.
/// </summary>
public enum AnalysisFreshness { Fresh, Stale, VeryStale, Unavailable }
public enum StaleReason { None, AgeOverWeek, GameInventoryChanged, WindowsBuildChanged, SchemaMismatch, Corrupted }

public sealed class AnalysisStateStore
{
    public const int SchemaVersion = 3;
    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly string _mutexName;
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
        string? CorrelationId,
        string? InstalledGamesFingerprint = null
    );

    public AnalysisStateStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(CaOPaths.ProgramDataRoot, "analysis-state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        _mutexName = @"Global\CA-O-Analysis-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(_filePath)))[..8];
    }
    private IDisposable AcquireGlobalLock()
    {
        var m = new System.Threading.Mutex(false, _mutexName);
        try { m.WaitOne(TimeSpan.FromSeconds(5)); } catch (AbandonedMutexException) { }
        return new MutexReleaser(m);
    }
    private sealed class MutexReleaser : IDisposable { private readonly System.Threading.Mutex _m; public MutexReleaser(System.Threading.Mutex m)=>_m=m; public void Dispose(){ try{_m.ReleaseMutex();}catch{} _m.Dispose();} }

    public void SaveAnalysis(PersistedAnalysis analysis)
    {
        lock (_lock)
        using (AcquireGlobalLock())
        {
            var tmp = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(analysis, JsonOpts);
            using (var stream = File.Create(tmp))
            using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            // Atomic replace
            File.Move(tmp, _filePath, overwrite: true);
        }
    }

    public bool HasAnalysis()
    {
        lock (_lock) using (AcquireGlobalLock()) { return File.Exists(_filePath); }
    }

    public PersistedAnalysis? LoadLatestAnalysis()
    {
        lock (_lock)
        using (AcquireGlobalLock())
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
        lock (_lock) using (AcquireGlobalLock()) { try { if (File.Exists(_filePath)) File.Delete(_filePath); } catch { } }
    }

    public PersistedAnalysis? Validate()
    {
        var loaded = LoadLatestAnalysis();
        if (loaded is null) return null;
        return loaded;
    }

    public bool IsObsolete(PersistedAnalysis analysis, SystemContext? currentContext = null)
    {
        var age = DateTime.UtcNow - analysis.TimestampUtc;
        if (age > TimeSpan.FromDays(7)) return true;
        if (currentContext != null && analysis.Context != null && analysis.Context.WindowsBuild != currentContext.WindowsBuild) return true;
        if (!string.IsNullOrEmpty(analysis.InstalledGamesFingerprint) && currentContext != null)
        {
            var currentFp = ComputeGamesFingerprint(currentContext.GamesDetected);
            if (currentFp != analysis.InstalledGamesFingerprint) return true;
        }
        return false;
    }

    public (AnalysisFreshness Freshness, StaleReason Reason, TimeSpan Age) GetFreshness(PersistedAnalysis? analysis, SystemContext? currentContext = null, string? currentGamesFingerprint = null)
    {
        if (analysis is null) return (AnalysisFreshness.Unavailable, StaleReason.None, TimeSpan.Zero);
        var age = DateTime.UtcNow - analysis.TimestampUtc;
        if (analysis.SchemaVersion != SchemaVersion) return (AnalysisFreshness.VeryStale, StaleReason.SchemaMismatch, age);
        if (!string.IsNullOrEmpty(analysis.InstalledGamesFingerprint) && !string.IsNullOrEmpty(currentGamesFingerprint) && analysis.InstalledGamesFingerprint != currentGamesFingerprint)
            return (AnalysisFreshness.Stale, StaleReason.GameInventoryChanged, age);
        if (currentContext != null && analysis.Context != null && analysis.Context.WindowsBuild != currentContext.WindowsBuild)
            return (AnalysisFreshness.Stale, StaleReason.WindowsBuildChanged, age);
        if (age > TimeSpan.FromDays(14)) return (AnalysisFreshness.VeryStale, StaleReason.AgeOverWeek, age);
        if (age > TimeSpan.FromDays(7)) return (AnalysisFreshness.Stale, StaleReason.AgeOverWeek, age);
        return (AnalysisFreshness.Fresh, StaleReason.None, age);
    }

    public static string ComputeGamesFingerprint(IReadOnlyList<string> games)
    {
        if (games == null || games.Count == 0) return "empty";
        var sorted = games.OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToArray();
        var joined = string.Join("|", sorted);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash)[..16];
    }

    public string GetStatusLabel(PersistedAnalysis? analysis)
    {
        if (analysis is null) return "Nunca analizado";
        var age = DateTime.UtcNow - analysis.TimestampUtc;
        if (age < TimeSpan.FromMinutes(5)) return $"Analizado hace {Math.Max(1, (int)age.TotalMinutes)} minutos";
        if (age < TimeSpan.FromHours(24)) return $"Analizado hace {(int)age.TotalHours} horas";
        if (age < TimeSpan.FromDays(7)) return $"Analizado hace {(int)age.TotalDays} días";
        if (age < TimeSpan.FromHours(48)) return "Analizado ayer";
        return analysis.AnalysisState == "CompletedWithWarnings" ? "Análisis completado con advertencias" 
            : analysis.AnalysisState == "Failed" ? "Análisis fallido"
            : analysis.AnalysisState == "Cancelled" ? "Análisis cancelado"
            : "Análisis obsoleto";
    }

    public string GetFreshnessLabel(AnalysisFreshness freshness, StaleReason reason, TimeSpan age)
    {
        return freshness switch
        {
            AnalysisFreshness.Fresh => "Análisis actualizado",
            AnalysisFreshness.Stale when reason == StaleReason.GameInventoryChanged => "Se detectaron cambios en los juegos instalados. Se recomienda ejecutar un nuevo análisis.",
            AnalysisFreshness.Stale => "Se recomienda ejecutar un análisis nuevo",
            AnalysisFreshness.VeryStale => "Este análisis es antiguo. Ejecuta un análisis nuevo antes de optimizar.",
            AnalysisFreshness.Unavailable => "No hay un análisis guardado.",
            _ => "Análisis obsoleto"
        };
    }
}
