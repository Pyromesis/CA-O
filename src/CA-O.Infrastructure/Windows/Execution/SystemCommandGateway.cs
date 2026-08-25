using System.Diagnostics;
using CAO.Core.Interfaces;
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
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

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
        timeoutCts.CancelAfter(DefaultTimeout);

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

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new PrivilegedCommandResult(process.ExitCode, stdout.Trim(), stderr.Trim(), TimedOut: false);
    }

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
