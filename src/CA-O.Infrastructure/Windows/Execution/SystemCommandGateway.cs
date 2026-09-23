using System.Diagnostics;
using CAO.Core.Interfaces;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Infrastructure.Windows.Execution;

/// <summary>
/// Windows implementation of the privileged execution funnel (FASE 4).
///
/// Pipeline per call: policy validation → canonical absolute path →
/// argument token check (already enforced by policy) → timeout/cancellation
/// → execute with UseShellExecute=false → capture output → normalize.
/// No shell is involved, no working directory override, no PATH resolution:
/// the file name handed to CreateProcess is always an absolute
/// %SystemRoot%-rooted path resolved by CommandPolicy.
/// </summary>
public sealed class SystemCommandGateway : IPrivilegedCommandExecutor
{
    // Techos centralizados en CAO.Shared.TimeoutProfile (fuente única junto
    // al servicio y al cliente UI): divergir aquí mataba DISM/defrag a medias.
    private static readonly TimeSpan DefaultTimeout = TimeoutProfile.DispatchDefault;
    private static readonly TimeSpan HeavyTimeout = TimeoutProfile.DispatchHeavy;
    private const int MaxOutputChars = 256 * 1024;

    // Operaciones que necesitan minutos (DISM/defrag): matarlas a los 60 s
    // las dejaba a medias y podía corromper el almacén de componentes.
    // Espejo del HeavyOptimizationIds del servicio a nivel de comando.
    private static TimeSpan TimeoutFor(SystemCommandKey key) => key switch
    {
        SystemCommandKey.DismStartComponentCleanup => HeavyTimeout,
        SystemCommandKey.DismResetBase => HeavyTimeout,
        SystemCommandKey.DefragC => HeavyTimeout,
        SystemCommandKey.DefragHdd => HeavyTimeout,
        SystemCommandKey.DefragRetrim => HeavyTimeout,
        SystemCommandKey.DefragAnalyze => HeavyTimeout,
        SystemCommandKey.PnPUtilAddDriver => HeavyTimeout,
        SystemCommandKey.ExpandCab => HeavyTimeout,
        _ => DefaultTimeout,
    };

    public async Task<PrivilegedCommandResult> ExecuteAsync(
        SystemCommandKey key,
        IReadOnlyList<string> arguments,
        CancellationToken ct = default)
    {
        var fileName = CommandPolicy.Resolve(key, arguments)
            ?? throw new UnauthorizedAccessException(
                $"CAO-SEC-010: comando no permitido por la política ({key}).");

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            // Arguments are added as explicit tokens; no quoting games.
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeoutFor(key));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            TryKill(process);
            return new PrivilegedCommandResult(-1, "", "Timed out", TimedOut: true);
        }

        var stdout = Truncate(await stdoutTask);
        var stderr = Truncate(await stderrTask);
        return new PrivilegedCommandResult(process.ExitCode, stdout.Trim(), stderr.Trim(), TimedOut: false);
    }

    // Cota de salida: schtasks /Query /V o pnputil /enum-devices pueden
    // devolver MBs; sin techo, la respuesta IPC hereda el OOM.
    private static string Truncate(string text) =>
        text.Length <= MaxOutputChars ? text : text[..MaxOutputChars];

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already gone.
        }
    }
}
