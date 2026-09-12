using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Storage;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Limpieza de cachés de apps: solo dirs de caché, nunca sesiones ni música
/// offline; apps abiertas se omiten. Tests con perfiles temporales falsos.
/// </summary>
public sealed class AppCacheCleanupTests
{
    [Fact]
    public void Targets_NeverTouchSessionsOrOfflineMusic()
    {
        var banned = new[] { "Local Storage", "IndexedDB", "Storage" };
        foreach (var target in CleanupAppCaches.Targets)
        {
            Assert.NotEmpty(target.CacheSubdirs);
            foreach (var sub in target.CacheSubdirs)
            {
                var leaf = sub.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Last();
                Assert.DoesNotContain(leaf, banned, StringComparer.OrdinalIgnoreCase);
            }
            Assert.NotEmpty(target.ProcessNames);
        }
        Assert.True(CleanupAppCaches.Targets.Count >= 3);
    }

    private static string MakeProfile(params (string Sub, int Files, int BytesEach)[] entries)
    {
        var root = Path.Combine(Path.GetTempPath(), "CA-O-Test-" + Guid.NewGuid().ToString("N"));
        foreach (var (sub, files, bytesEach) in entries)
        {
            var dir = Directory.CreateDirectory(Path.Combine(root, sub));
            for (int i = 0; i < files; i++)
                File.WriteAllBytes(Path.Combine(dir.FullName, $"f{i}.bin"), new byte[bytesEach]);
        }
        return root;
    }

    [Fact]
    public void CacheBytesFor_SumsRecursively()
    {
        var root = MakeProfile(("Cache", 3, 1024), ("Code Cache", 2, 2048), ("Other", 5, 4096));
        try
        {
            Assert.Equal(3 * 1024 + 2 * 2048,
                CleanupAppCaches.CacheBytesFor(root, ["Cache", "Code Cache"]));
            Assert.Equal(0, CleanupAppCaches.CacheBytesFor(Path.Combine(root, "NoExiste"), ["Cache"]));
            Assert.Equal(0, CleanupAppCaches.CacheBytesFor("", ["Cache"]));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void DeleteCacheFiles_RemovesFilesKeepsDirs()
    {
        var root = MakeProfile(("Cache", 3, 100), ("Local Storage", 2, 100));
        try
        {
            var (files, bytes) = CleanupAppCaches.DeleteCacheFiles(root, ["Cache"], CancellationToken.None);
            Assert.Equal(3, files);
            Assert.Equal(300, bytes);
            Assert.True(Directory.Exists(Path.Combine(root, "Cache")));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(root, "Local Storage"), "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void DeleteCacheFiles_MissingDirYieldsZero()
    {
        var (files, bytes) = CleanupAppCaches.DeleteCacheFiles(
            Path.Combine(Path.GetTempPath(), "CA-O-NoExiste-" + Guid.NewGuid().ToString("N")),
            ["Cache"], CancellationToken.None);
        Assert.Equal(0, files);
        Assert.Equal(0, bytes);
    }

    [Theory]
    [InlineData(new[] { "discord" }, true)]
    [InlineData(new[] { "DISCORD" }, true)]
    [InlineData(new[] { "explorer", "notepad" }, false)]
    [InlineData(new string[0], false)]
    public void AnyProcessRunning_MatchesCaseInsensitively(string[] running, bool expected)
    {
        Assert.Equal(expected,
            CleanupAppCaches.AnyProcessRunning(["Discord", "Spotify"], () => running));
    }

    [Fact]
    public void Detect_NeverThrowsAndReturnsValidState()
    {
        var state = new CleanupAppCaches().Detect(new MemoryRegistry());
        Assert.True(state is OptimizationState.AppliedByCao or OptimizationState.NotApplied);
    }

    [Fact]
    public void Definition_IsMaintenanceStorageEntry()
    {
        var definition = new CleanupAppCaches().Definition;
        Assert.Equal("cleanup-app-caches", definition.Id);
        Assert.True(definition.Flags.HasFlag(OptimizationFlags.NotReversible));
        Assert.Equal(OptimizationCategory.Storage, definition.Category);
        Assert.NotEqual(EvidenceLevel.Unknown, definition.Evidence);
    }
}
