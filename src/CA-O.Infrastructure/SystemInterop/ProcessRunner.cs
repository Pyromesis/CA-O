using System.Diagnostics;
using CAO.Core.Abstractions;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>Runs elevated tools (powercfg, netsh, bcdedit) with timeouts.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<(int ExitCode, string Output)> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            return (-1, "Process timed out after 60s");
        }

        var output = (await outputTask + Environment.NewLine + await errorTask).Trim();
        return (process.ExitCode, output);
    }
}
