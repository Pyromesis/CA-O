using CAO.Shared;

namespace CAO.Core.Engine;

/// <summary>Aggregated internal health of the optimizer itself (FASE 34).</summary>
public sealed record ObservabilityMetrics
{
    public required int TotalTransactions { get; init; }

    public required int Committed { get; init; }

    public required int RolledBack { get; init; }

    public required int Failed { get; init; }

    /// <summary>Rollbacks whose re-detect could NOT confirm the original state.</summary>
    public required int RollbackNotVerified { get; init; }

    public required double RollbackFailureRate { get; init; }

    /// <summary>Verifications ending Unknown relative to all verifications (FASE 10 honesty).</summary>
    public required double VerificationUnknownRate { get; init; }

    public required double FailureRate { get; init; }

    public static ObservabilityMetrics FromJournal(IEnumerable<(Guid TxId, IReadOnlyList<TransactionEvent> Events)> groups)
    {
        var transactions = groups
            .Where(group => group.Events.Count > 0)
            .Select(group => group.Events[^1])
            .ToList();

        int committed = 0, rolledBack = 0, failed = 0, rollbackNotVerified = 0;

        foreach (var last in transactions)
        {
            switch (last.Phase)
            {
                case TransactionPhase.Commit:
                    committed++;
                    break;
                case TransactionPhase.RolledBack:
                    rolledBack++;
                    // Terminal RolledBack with an error code means the
                    // rollback itself could not be verified.
                    if (!string.IsNullOrWhiteSpace(last.ErrorCode) &&
                        last.ErrorCode.StartsWith("CAO-ROLLBACK", StringComparison.Ordinal))
                    {
                        rollbackNotVerified++;
                    }
                    break;
                default:
                    failed++;
                    break;
            }
        }

        var total = transactions.Count;
        return new ObservabilityMetrics
        {
            TotalTransactions = total,
            Committed = committed,
            RolledBack = rolledBack,
            Failed = failed,
            RollbackNotVerified = rollbackNotVerified,
            RollbackFailureRate = rolledBack == 0 ? 0 : Math.Round((double)rollbackNotVerified / rolledBack * 100, 2),
            VerificationUnknownRate = total == 0 ? 0 :
                Math.Round((double)transactions.Count(last =>
                    last.ErrorCode is not null && last.ErrorCode.StartsWith("CAO-VERIFY-002")) / total * 100, 2),
            FailureRate = total == 0 ? 0 : Math.Round((double)(failed + rolledBack) / total * 100, 2),
        };
    }
}
