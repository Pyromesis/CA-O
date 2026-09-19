using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Storage;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>Las 5 limpiezas nuevas: solo rutas seguras, nunca sesiones ni datos.</summary>
public sealed class NewCleanupsSafetyTests
{
    [Fact]
    public async Task BrowserCache_NeverTouchesSessionsOrPasswords()
    {
        var banned = new[] { "Local Storage", "IndexedDB", "Session Storage", "Login Data", "History", "Bookmarks", "Storage" };
        var preview = await new CleanupBrowserCodeCache()
            .PreviewAsync(new MemoryRegistry(), CancellationToken.None);
        Assert.NotEmpty(preview.Lines);
        foreach (var line in preview.Lines)
        {
            var leaf = line.Target.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Last();
            Assert.DoesNotContain(leaf, banned, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CrashDumpsExtended_OnlyDumpPatterns()
    {
        var preview = await new CleanupCrashDumpsExtended()
            .PreviewAsync(new MemoryRegistry(), CancellationToken.None);
        Assert.NotEmpty(preview.Lines);
        // Contains (no EndsWith): PreviewLine.Target lleva sufijo " (>Nd)".
        foreach (var line in preview.Lines)
            Assert.True(line.Target.Contains("*.dmp", StringComparison.OrdinalIgnoreCase)
                || line.Target.Contains("MEMORY.DMP", StringComparison.OrdinalIgnoreCase),
                $"patrón inesperado: {line.Target}");
    }

    [Fact]
    public async Task PrefetchAndCbs_OnlyTheirPatterns()
    {
        var prefetch = await new CleanupPrefetchStale()
            .PreviewAsync(new MemoryRegistry(), CancellationToken.None);
        Assert.All(prefetch.Lines, l => Assert.Contains("*.pf", l.Target, StringComparison.Ordinal));
        var cbs = await new CleanupCbsLogs()
            .PreviewAsync(new MemoryRegistry(), CancellationToken.None);
        Assert.All(cbs.Lines, l => Assert.Contains("*.log", l.Target, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OutlookCache_OnlyContentOutlook()
    {
        var preview = await new CleanupOutlookCache()
            .PreviewAsync(new MemoryRegistry(), CancellationToken.None);
        foreach (var line in preview.Lines)
            Assert.Contains("Content.Outlook", line.Target, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NewCleanups_AreStorageNotReversible()
    {
        IOptimization[] opts =
        [
            new CleanupPrefetchStale(), new CleanupCbsLogs(), new CleanupCrashDumpsExtended(),
            new CleanupOutlookCache(), new CleanupBrowserCodeCache(),
        ];
        var ids = new[] { "cleanup-prefetch-stale", "cleanup-cbs-logs", "cleanup-crash-dumps-extended", "cleanup-outlook-cache", "cleanup-browser-code-cache" };
        Assert.Equal(ids, opts.Select(o => o.Definition.Id));
        foreach (var o in opts)
        {
            Assert.Equal(OptimizationCategory.Storage, o.Definition.Category);
            Assert.True(o.Definition.Flags.HasFlag(OptimizationFlags.NotReversible));
        }
    }
}
