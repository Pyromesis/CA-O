using CAO.Core.Engine;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Integration.Tests;

/// <summary>Observability metrics from transaction journals (FASE 34).</summary>
public sealed class ObservabilityMetricsTests
{
    private static TransactionEvent E(Guid tx, string opt, TransactionPhase phase,
        bool terminal = false, string? code = null, int minutesAgo = 0) =>
        new(tx, opt, DateTime.UtcNow.AddMinutes(-minutesAgo), phase, terminal, code);

    [Fact]
    public void EmptyJournalYieldsZeroes()
    {
        var metrics = ObservabilityMetrics.FromJournal([]);

        Assert.Equal(0, metrics.TotalTransactions);
        Assert.Equal(0, metrics.RollbackFailureRate);
        Assert.Equal(0, metrics.FailureRate);
    }

    [Fact]
    public void OutcomeCountersReflectTerminalPhases()
    {
        var tx1 = Guid.NewGuid(); // committed
        var tx2 = Guid.NewGuid(); // rolled back cleanly
        var tx3 = Guid.NewGuid(); // rolled back WITHOUT verification
        var tx4 = Guid.NewGuid(); // failed

        var groups = new[]
        {
            (tx1, (IReadOnlyList<TransactionEvent>)new List<TransactionEvent>
            {
                E(tx1,"a",TransactionPhase.Snapshot), E(tx1,"a",TransactionPhase.Commit,true),
            }),
            (tx2, (IReadOnlyList<TransactionEvent>)new List<TransactionEvent>
            {
                E(tx2,"a",TransactionPhase.Snapshot), E(tx2,"a",TransactionPhase.RolledBack,true),
            }),
            (tx3, (IReadOnlyList<TransactionEvent>)new List<TransactionEvent>
            {
                E(tx3,"a",TransactionPhase.Snapshot),
                E(tx3,"a",TransactionPhase.RolledBack,true,"CAO-ROLLBACK-001"),
            }),
            (tx4, (IReadOnlyList<TransactionEvent>)new List<TransactionEvent>
            {
                E(tx4,"a",TransactionPhase.Failed,true),
            }),
        };

        var metrics = ObservabilityMetrics.FromJournal(groups);

        Assert.Equal(4, metrics.TotalTransactions);
        Assert.Equal(1, metrics.Committed);
        Assert.Equal(2, metrics.RolledBack);
        Assert.Equal(1, metrics.Failed);
        Assert.Equal(1, metrics.RollbackNotVerified);
        Assert.Equal(50.0, metrics.RollbackFailureRate);
        Assert.Equal(75.0, metrics.FailureRate); // failed + rolledBack
    }
}
