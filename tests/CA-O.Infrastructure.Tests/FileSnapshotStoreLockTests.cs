using CAO.Infrastructure.Persistence;
using Xunit;

namespace CAO.Infrastructure.Tests;

public sealed class FileSnapshotStoreLockTests
{
    /// <summary>
    /// CAO-BUG-2026-10-06: <c>AcquireGlobalLock</c> descartaba el bool devuelto por
    /// <c>WaitOne</c>. Al agotarse el tiempo de espera el trabajo continuaba SIN
    /// proteccion y el <c>ReleaseMutex</c> sobre un mutex no poseido se perdia en un
    /// <c>catch</c> vacio. Perder la exclusion en silencio permite que otra instancia
    /// de CA-O borre o corrompa un snapshot mientras este lo verifica. El fallo debe
    /// ser explicito, no silencioso.
    /// </summary>
    [Fact]
    public void TryLoad_ThrowsTimeout_WhenTheGlobalLockIsHeldByAnotherThread()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cao-snap-lock-{Guid.NewGuid():N}");
        var store = new FileSnapshotStore(root);
        var previousTimeout = FileSnapshotStore.GlobalLockTimeout;
        FileSnapshotStore.GlobalLockTimeout = TimeSpan.FromMilliseconds(250);

        using var blocker = new Mutex(false, store.GlobalMutexName);
        using var holderReady = new ManualResetEventSlim(false);
        using var releaseHolder = new ManualResetEventSlim(false);

        // Un mutex es afin a hilo y el mismo hilo puede re-adquirirlo de forma
        // recursiva, asi que el bloqueo debe sostenerlo OTRO hilo o el test no
        // probaria nada.
        var holder = new Thread(() =>
        {
            blocker.WaitOne(TimeSpan.FromSeconds(10));
            holderReady.Set();
            releaseHolder.Wait(TimeSpan.FromSeconds(30));
            try { blocker.ReleaseMutex(); } catch { }
        })
        { IsBackground = true };

        try
        {
            holder.Start();
            Assert.True(holderReady.Wait(TimeSpan.FromSeconds(10)),
                "El hilo auxiliar no pudo tomar el mutex global.");

            var ex = Assert.Throws<TimeoutException>(() => store.TryLoad(Guid.NewGuid(), out _));
            Assert.Contains("snapshots", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            releaseHolder.Set();
            holder.Join(TimeSpan.FromSeconds(10));
            FileSnapshotStore.GlobalLockTimeout = previousTimeout;
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
        }
    }
}