using CAO.Core.Interfaces;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Infrastructure.Windows.Etw;

/// <summary>
/// WPR-backed ETW collector for kernel DPC/ISR events (FASE 20).
///
/// Guarantees:
///   - single concurrent session (semaphore);
///   - fixed profile ("CPU") and fixed output under %ProgramData%\CA-O\etw;
///   - every start is paired with a stop+delete attempt in finally, so a
///     crash of the caller never leaks a running trace session;
///   - execution goes exclusively through the privileged gateway policy.
///
/// Honest limitation: parsing/ranking per-DRIVER requires kernel stackwalk
/// decoding (Microsoft.Windows.EventTracing). This collector delivers the
/// validated ETL; attribution remains at CPU level in DpcLatencySampler.
/// </summary>
public sealed class WprDpcCollector : IDpcEtwCollector
{
    private static readonly SemaphoreSlim Session = new(1, 1);

    public static string OutputDirectory =>
        Path.Combine(CaOPaths.ProgramDataRoot, "etw");

    public static string DefaultEtlPath => Path.Combine(OutputDirectory, "dpc.etl");

    private readonly IPrivilegedCommandExecutor _executor;

    public WprDpcCollector(IPrivilegedCommandExecutor executor) => _executor = executor;

    public async Task<EtwCollectionResult> CollectAsync(
        TimeSpan duration,
        CancellationToken ct = default)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(duration),
                "La ventana ETW debe estar entre 1 s y 5 min (límite FASE 20).");
        }

        await Session.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(OutputDirectory);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var start = await _executor.ExecuteAsync(SystemCommandKey.WprStartCpuFileMode,
                ["-start", "CPU", "-filemode"], ct);
            if (!start.Success)
            {
                throw new InvalidOperationException("No se pudo iniciar la traza ETW: " + start.StdErr);
            }

            string etl = DefaultEtlPath;
            var cleanStop = false;
            try
            {
                await Task.Delay(duration, ct);
                var stop = await _executor.ExecuteAsync(SystemCommandKey.WprStopToDefaultFile,
                    ["-stop", etl, "-overwrite"], ct);
                cleanStop = stop.Success;
                if (!cleanStop)
                {
                    throw new InvalidOperationException("No se pudo detener la traza ETW: " + stop.StdErr);
                }
                sw.Stop();
                return new EtwCollectionResult(etl, duration, CleanStop: true);
            }
            catch
            {
                // Never leave a live trace behind on failure paths.
                await BestEffortDeleteSessionAsync();
                if (System.IO.File.Exists(etl))
                {
                    TryDelete(etl);
                }
                throw;
            }
            finally
            {
                if (!cleanStop && !ct.IsCancellationRequested)
                {
                    await BestEffortDeleteSessionAsync();
                }
            }
        }
        finally
        {
            Session.Release();
        }
    }

    private async Task BestEffortDeleteSessionAsync()
    {
        try
        {
            await _executor.ExecuteAsync(SystemCommandKey.LogmanDeleteSession,
                ["delete", "CAO-DPC", "-ets"]);
        }
        catch
        {
            // Best effort by contract.
        }
    }

    private static void TryDelete(string file)
    {
        try { System.IO.File.Delete(file); } catch { /* best effort */ }
    }
}
