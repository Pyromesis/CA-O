namespace CAO.Core.Interfaces;

/// <summary>Result payload of an ETW collection window.</summary>
public sealed record EtwCollectionResult(string EtlPath, TimeSpan Duration, bool CleanStop);

/// <summary>
/// Optional ETW DPC/ISR tracer (FASE 20): StartTrace → Collect → StopTrace
/// with strict duration/size limits and guaranteed cleanup. Parsing and
/// per-driver ranking require the Microsoft.Windows.EventTracing package and
/// remain a documented roadmap item — this interface only guarantees the
/// lifecycle.
/// </summary>
public interface IDpcEtwCollector
{
    /// <summary>
    /// Collects a kernel DPC/ISR trace of the requested duration into the
    /// standard ETL location. Throws when a session is already active or a
    /// lifecycle command fails; never leaves a session running.
    /// </summary>
    Task<EtwCollectionResult> CollectAsync(TimeSpan duration, CancellationToken ct = default);
}
