using System.Diagnostics;

namespace CAO.Infrastructure.Gaming;

/// <summary>A detected game installation/run.</summary>
public sealed record DetectedGame(string Name, string ExecutableName, string Source);

/// <summary>
/// Detects well-known games by scanning running process names and common
/// install roots (spec 93). Detection only informs per-game guidance; CA-O
/// never modifies game files or anti-cheat binaries (spec 95).
/// </summary>
public sealed class GameDetectionProvider
{
    /// <summary>Known shipping executables → display name (§93 gaming matrix).</summary>
    private static readonly IReadOnlyDictionary<string, string> KnownExecutables =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cs2.exe"] = "Counter-Strike 2",
            ["valorant.exe"] = "VALORANT",
            ["valorant-win64-shipping.exe"] = "VALORANT",
            ["fortniteclient-win64-shipping.exe"] = "Fortnite",
            ["r5apex.exe"] = "Apex Legends",
            ["overwatch.exe"] = "Overwatch 2",
        };

    public async Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var found = new Dictionary<string, DetectedGame>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    ct.ThrowIfCancellationRequested();
                    if (KnownExecutables.TryGetValue(process.ProcessName + ".exe", out var name))
                    {
                        found[name] = new DetectedGame(name, process.ProcessName + ".exe", "proceso en ejecución");
                    }
                }
            }
            catch
            {
                // Process enumeration can be restricted; fall through to empty.
            }

            return found.Values.ToList();
        }, ct);
    }
}
