using CAO.Infrastructure.Persistence;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Ciclo de vida del journal de transacciones.
/// CAO-BUG-2026-10-06: el journal es un log de solo-anexado sin politica de
/// retencion. Medido en esta maquina: 102 ficheros, 68 KB, 286 lineas, y
/// <c>LoadAll</c> tardaba 75 ms en releerlo entero. Esa lectura ocurre en cada
/// apply (guarda de recuperacion), en cada revert y en cada "Analizar" del
/// Dashboard, en el hilo de la UI. Los ficheros ya cerrados no los lee nadie:
/// la unica consulta del journal es <c>Incomplete()</c>, que los descarta. Sin
/// poda el directorio crece sin limite y el coste crece con el.
/// </summary>
public sealed class TransactionJournalRetentionTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "cao-journal-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static TransactionEvent Event(Guid tx, TransactionPhase phase) =>
        new(tx, "test-optimization", DateTime.UtcNow, phase,
            TransactionEvent.IsTerminal(phase), null, "S-1-5-18", "tester");

    private string FileFor(Guid tx) =>
        Path.Combine(_dir, tx.ToString("D") + ".jsonl");

    [Fact]
    public void Prune_RemovesClosedTransactions_ButNeverUnfinishedOnes()
    {
        var journal = new FileTransactionJournal(_dir);
        var unfinished = Guid.NewGuid();
        var closed = Guid.NewGuid();
        var trigger = Guid.NewGuid();

        journal.Append(Event(unfinished, TransactionPhase.BenchmarkCompleted));
        journal.Append(Event(closed, TransactionPhase.Commit));

        // Ambos quedan fuera de la ventana de retencion por defecto (30 dias).
        var old = DateTime.UtcNow - TimeSpan.FromDays(60);
        File.SetLastWriteTimeUtc(FileFor(unfinished), old);
        File.SetLastWriteTimeUtc(FileFor(closed), old);
        Assert.True(File.Exists(FileFor(closed)));

        // Solo se poda al anexar un evento terminal, no en cada linea.
        journal.Append(Event(trigger, TransactionPhase.Commit));

        Assert.False(File.Exists(FileFor(closed)),
            "Una transaccion ya cerrada y fuera de retencion no se relee nunca: debe podarse.");
        Assert.True(File.Exists(FileFor(unfinished)),
            "Una transaccion sin cierre es precisamente lo que la recuperacion necesita: nunca se poda.");
        Assert.True(File.Exists(FileFor(trigger)),
            "La transaccion recien cerrada sigue dentro de la ventana: se conserva.");
    }

    [Fact]
    public void LoadAll_ReusesTheParsedLog_UntilTheJournalChanges()
    {
        var journal = new FileTransactionJournal(_dir);
        journal.Append(Event(Guid.NewGuid(), TransactionPhase.Commit));

        var before = FileTransactionJournal.ParsePasses;
        journal.LoadAll();
        var afterFirst = FileTransactionJournal.ParsePasses;
        Assert.True(afterFirst > before, "La primera lectura tiene que leer el disco.");

        journal.LoadAll();
        journal.LoadAll();
        Assert.Equal(afterFirst, FileTransactionJournal.ParsePasses);

        journal.Append(Event(Guid.NewGuid(), TransactionPhase.Commit));
        journal.LoadAll();
        Assert.True(FileTransactionJournal.ParsePasses > afterFirst,
            "Un evento nuevo invalida la lectura memorizada.");
    }
}
