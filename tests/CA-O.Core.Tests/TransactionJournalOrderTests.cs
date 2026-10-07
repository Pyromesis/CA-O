using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// CAO-BUG-2026-10-06 (F3): el journal registraba <c>Commit</c> ANTES de las fases de
/// benchmark, y ni <c>BenchmarkCompleted</c> ni <c>BenchmarkFailed</c> son terminales.
/// Como "incompleta" se decide por el ULTIMO evento anadido, toda transaccion exitosa
/// quedaba permanentemente incompleta: el historico real acumulo 88 de 98 ficheros con
/// esa forma, y cada unoellia hacia que <c>CrashRecoveryService</c> volviera a mirar el
/// estado en vivo de cada uno. Estos tests fijan el orden correcto.
/// </summary>
public sealed class TransactionJournalOrderTests
{
    private static readonly SystemContext Context = SystemContextFactory.Default();

    private static OptimizationDefinition Definition(string id) => new()
    {
        Id = id,
        NameEs = "Prueba de orden",
        NameEn = "Order test",
        DescriptionEs = "Verifica el orden de las fases en el journal",
        DescriptionEn = "Verifies phase ordering in the journal",
        Evidence = EvidenceLevel.Benchmark,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Reversible = true,
        Flags = OptimizationFlags.None,
    };

    [Fact]
    public async Task SuccessfulApply_EndsWithTheTerminalCommitPhase_NotWithABenchmarkPhase()
    {
        var journal = new RecordingJournal();
        var stub = new StubOptimization(Definition("journal-order-opt"));

        var report = await new OptimizationTransaction(
            stub, new MemoryRegistry(), Context, journal: journal).RunAsync();

        Assert.True(report.Success, report.MessageEs);
        Assert.Equal(TransactionPhase.Commit, report.FinalPhase);
        Assert.Equal(TransactionPhase.Commit, journal.Events[^1].Phase);
    }

    [Fact]
    public async Task SuccessfulApply_LeavesNoUnfinishedTransaction_EvenThoughTheBenchmarkRuns()
    {
        var journal = new RecordingJournal();
        var stub = new StubOptimization(Definition("journal-order-opt"));

        var report = await new OptimizationTransaction(
            stub, new MemoryRegistry(), Context, journal: journal).RunAsync();

        Assert.True(report.Success, report.MessageEs);

        // El benchmark se ejecuta entre medias: si su "completed" queda como ultima
        // fase anadida, la transaccion parece incompleta para siempre.
        Assert.Contains(journal.Events, e => e.Phase == TransactionPhase.BenchmarkStarted);
        Assert.Contains(journal.Events, e => e.Phase == TransactionPhase.BenchmarkCompleted);
        Assert.Empty(journal.Incomplete());
    }

    /// <summary>Journal en memoria que conserva el orden de insercion.</summary>
    private sealed class RecordingJournal : ITransactionJournal
    {
        public List<TransactionEvent> Events { get; } = new();

        public void Append(TransactionEvent evt) => Events.Add(evt);

        public IReadOnlyList<(Guid TransactionId, IReadOnlyList<TransactionEvent> Events)> LoadAll()
        {
            var grouped = new List<(Guid, IReadOnlyList<TransactionEvent>)>();
            foreach (var group in Events.GroupBy(e => e.TransactionId))
                grouped.Add((group.Key, group.ToList()));
            return grouped;
        }
    }
}