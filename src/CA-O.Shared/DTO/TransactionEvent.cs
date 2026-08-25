using System.Text.Json.Serialization;

namespace CAO.Shared;

/// <summary>
/// One persisted transition of a transaction journal (FASE 5/12). Events are
/// append-only; a transaction without a terminal event is INCOMPLETE and is
/// picked up by crash recovery on service start.
/// </summary>
public sealed record TransactionEvent(
    [property: JsonPropertyName("tx")] Guid TransactionId,
    [property: JsonPropertyName("opt")] string OptimizationId,
    [property: JsonPropertyName("utc")] DateTime TimestampUtc,
    [property: JsonPropertyName("phase")] TransactionPhase Phase,
    [property: JsonPropertyName("terminal")] bool Terminal,
    [property: JsonPropertyName("code")] string? ErrorCode)
{
    /// <summary>Phases that close a transaction cleanly.</summary>
    public static bool IsTerminal(TransactionPhase phase) =>
        phase is TransactionPhase.Commit
              or TransactionPhase.RolledBack
              or TransactionPhase.Failed
              or TransactionPhase.RecoveryCompleted;
}
