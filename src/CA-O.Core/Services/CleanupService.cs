using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Services;

/// <summary>#8: temp files cleanup with real freed-bytes reporting.</summary>
public sealed class CleanupService
{
    private static readonly string[] DefaultRoots =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Temp"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
    };

    /// <summary>Roots allowed for cleanup; overridable in tests.</summary>
    public IReadOnlyList<string> Roots { get; }

    public CleanupService(IReadOnlyList<string>? roots = null)
    {
        var resolved = roots ?? DefaultRoots;
        Roots = resolved
            .Where(r => !string.IsNullOrWhiteSpace(r) && Path.IsPathRooted(r) && Path.GetPathRoot(r)!.Length > 3)
            .ToList();
    }

    public long EstimateBytes()
    {
        long total = 0;
        foreach (var root in Roots.Where(Directory.Exists))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(file).Length; }
                    catch { /* locked or gone */ }
                }
            }
            catch { /* access denied on some subtree */ }
        }
        return total;
    }

    public async Task<(long FreedBytes, int Errors)> CleanAsync(CancellationToken ct = default)
    {
        long freed = 0;
        int errors = 0;
        await Task.Run(() =>
        {
            foreach (var root in Roots.Where(Directory.Exists))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(root))
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        freed += DeleteTree(entry);
                    }
                    catch
                    {
                        errors++;
                    }
                }
            }
        }, ct);
        return (freed, errors);
    }

    private static long DeleteTree(string path)
    {
        if (Directory.Exists(path))
        {
            long bytes = 0;
            foreach (var child in Directory.EnumerateFileSystemEntries(path))
            {
                bytes += DeleteTree(child);
            }
            try { Directory.Delete(path, recursive: false); } catch { /* non-empty: children locked */ }
            return bytes;
        }

        var info = new FileInfo(path);
        var size = info.Length;
        info.Delete();
        return size;
    }
}
