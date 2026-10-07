using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// CAO-BUG-2026-10-06 (N-1): mientras el servicio detecta una recuperacion pendiente
/// rechaza tanto <c>ApplyAsync</c> como <c>RevertAsync</c> con
/// <c>CAO-TXN-004</c>, y <c>CrashRecoveryService.MarkRecovered</c> no lo llamaba
/// nadie: el equipo quedaba sin salida. Estas pruebas fijan la semántica de
/// <c>OptimizationEngine.RecoverAsync</c>, que es esa salida.
/// </summary>
public sealed class RecoverTransactionTests
{
    private sealed class InMemoryJournal : ITransactionJournal
    {
        public List<TransactionEvent> Events { get; } = new();

        public void Append(TransactionEvent evt) => Events.Add(evt);

        public IReadOnlyList<(Guid TransactionId, IReadOnlyList<TransactionEvent> Events)> LoadAll() =>
            Events
                .GroupBy(e => e.TransactionId)
                .Select(g => (g.Key, (IReadOnlyList<TransactionEvent>)g.ToList()))
                .ToList();
    }

    private static TransactionEvent Event(Guid tx, string optimizationId, TransactionPhase phase) =>
        new(tx, optimizationId, DateTime.UtcNow, phase, TransactionEvent.IsTerminal(phase), null, "S-1-5-18", "tester");

    private static void SaveSnapshot(MemorySnapshotStore store, Guid tx, string optimizationId)
    {
        store.Save(new TransactionSnapshotRecord
        {
            Manifest = new TransactionSnapshotManifest
            {
                TransactionId = tx,
                OptimizationId = optimizationId,
                DefinitionVersion = "1",
                SchemaVersion = 3,
                AppVersion = AppVersion.Semantic,
                WindowsBuild = 26200,
                TimestampUtc = DateTime.UtcNow
            },
            State = new OptimizationSnapshot()
        });
    }

    private static OptimizationEngine BuildEngine(
        CrashRecoveryService recovery, MemorySnapshotStore store) =>
        new(
            new MemoryRegistry(),
            null!,
            store,
            new MemoryHistory(),
            isRunningAsAdmin: () => true,
            recovery: recovery);

    /// <summary>
    /// Sin nada pendiente la operacion es un no-op correcto: repetirla no debe fallar,
    /// porque la UI puede ejecutarla dos veces o tras un cierre ya resuelto.
    /// </summary>
    [Fact]
    public async Task IsANoOp_WhenNothingIsPending()
    {
        var journal = new InMemoryJournal();
        var store = new MemorySnapshotStore();
        var recovery = new CrashRecoveryService(journal, store, _ => OptimizationState.AppliedByCao);

        var result = await BuildEngine(recovery, store).RecoverAsync(Guid.NewGuid());

        Assert.True(result.Success, result.MessageEs);
        Assert.Contains("ya no está pendiente", result.MessageEs, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(journal.Events);
    }

    /// <summary>
    /// Descartar cierra la entrada del journal y NO toca el sistema. Es la unica
    /// salida cuando el snapshot no permite una reverticion fiable, y por eso no
    /// debe borrar el snapshot ni intentar revertir.
    /// </summary>
    [Fact]
    public async Task Discard_ClosesTheJournal_WithoutTouchingTheSystem()
    {
        var tx = Guid.NewGuid();
        var journal = new InMemoryJournal();
        journal.Append(Event(tx, "cleanup-windows-temp", TransactionPhase.Snapshot));
        journal.Append(Event(tx, "cleanup-windows-temp", TransactionPhase.Apply));
        var store = new MemorySnapshotStore();
        SaveSnapshot(store, tx, "cleanup-windows-temp");
        var recovery = new CrashRecoveryService(journal, store, _ => OptimizationState.AppliedByCao);

        var result = await BuildEngine(recovery, store).RecoverAsync(tx, discardChanges: true);

        Assert.True(result.Success, result.MessageEs);
        Assert.Contains("NO se revirtieron", result.MessageEs, StringComparison.OrdinalIgnoreCase);
        var last = journal.Events[^1];
        Assert.Equal(TransactionPhase.RecoveryCompleted, last.Phase);
        Assert.True(last.Terminal);
        Assert.Equal(ErrorCodes.RollbackFailed, last.ErrorCode);
        Assert.True(store.Saved.ContainsKey(tx));
        Assert.DoesNotContain(tx, store.Deleted);
    }

    /// <summary>
    /// Con decision bloqueante por rollback se revierte desde el snapshot y la
    /// transaccion queda cerrada como recuperada, sin codigo de error.
    /// </summary>
    [Fact]
    public async Task RevertsAndCloses_WhenTheDecisionRequiresRollback()
    {
        var tx = Guid.NewGuid();
        var journal = new InMemoryJournal();
        journal.Append(Event(tx, "cleanup-windows-temp", TransactionPhase.Snapshot));
        journal.Append(Event(tx, "cleanup-windows-temp", TransactionPhase.Apply));
        var store = new MemorySnapshotStore();
        SaveSnapshot(store, tx, "cleanup-windows-temp");
        var recovery = new CrashRecoveryService(journal, store, _ => OptimizationState.AppliedByCao);

        var result = await BuildEngine(recovery, store).RecoverAsync(tx);

        Assert.True(result.Success, result.MessageEs);
        Assert.Contains("recuperada", result.MessageEs, StringComparison.OrdinalIgnoreCase);
        var last = journal.Events[^1];
        Assert.Equal(TransactionPhase.RecoveryCompleted, last.Phase);
        Assert.True(last.Terminal);
        Assert.Null(last.ErrorCode);
        Assert.Contains(tx, store.Deleted);
        Assert.False(recovery.HasPendingRecovery());
    }

    /// <summary>
    /// Una transaccion sin snapshot llega como <c>Corrupted</c>: no hay nada fiable
    /// que revertir, asi que se informa y se deja descartar en su lugar. Cerrarla en
    /// silencio seria mentir sobre el estado del sistema.
    /// </summary>
    [Fact]
    public async Task RefusesWhenThereIsNoReliableSnapshot()
    {
        var tx = Guid.NewGuid();
        var journal = new InMemoryJournal();
        journal.Append(Event(tx, "cleanup-windows-temp", TransactionPhase.Snapshot));
        journal.Append(Event(tx, "cleanup-windows-temp", TransactionPhase.Apply));
        var store = new MemorySnapshotStore();
        var recovery = new CrashRecoveryService(journal, store, _ => OptimizationState.AppliedByCao);

        var result = await BuildEngine(recovery, store).RecoverAsync(tx);

        Assert.False(result.Success);
        Assert.Equal("recovery-not-reversible", result.Error);
        Assert.DoesNotContain(journal.Events, e => e.Phase == TransactionPhase.RecoveryCompleted);
    }

    /// <summary>
    /// Si el rollback ni siquiera puede arrancar, la transaccion se queda ABIERTA a
    /// proposito: cerrarla en falso dejaria cambios a medias sin rastro y el usuario
    /// perderia la posibilidad de reintentar o descartar.
    /// </summary>
    [Fact]
    public async Task LeavesTheTransactionOpen_WhenTheRollbackCannotStart()
    {
        var tx = Guid.NewGuid();
        var journal = new InMemoryJournal();
        journal.Append(Event(tx, "optimizacion-inexistente", TransactionPhase.Snapshot));
        journal.Append(Event(tx, "optimizacion-inexistente", TransactionPhase.Apply));
        var store = new MemorySnapshotStore();
        SaveSnapshot(store, tx, "optimizacion-inexistente");
        var recovery = new CrashRecoveryService(journal, store, _ => OptimizationState.AppliedByCao);

        var result = await BuildEngine(recovery, store).RecoverAsync(tx);

        Assert.False(result.Success);
        Assert.DoesNotContain(journal.Events, e => e.Phase == TransactionPhase.RecoveryCompleted);
        Assert.True(recovery.HasPendingRecovery(), "Una recuperacion no resuelta debe seguir bloqueando el equipo, no cerrarse en falso.");
    }
}
