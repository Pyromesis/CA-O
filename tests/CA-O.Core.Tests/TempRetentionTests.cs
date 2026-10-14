using CAO.Core.Optimizations.Storage;

using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Frontera de retención de 10 minutos en las limpiezas de temporales reales
/// que NO heredan de <c>TempFileCleanupOptimization</c> (borraban todo sin
/// mirar la edad): fichero de 11 min cae, de 9 min se respeta. Cada clase
/// comparte la fuente única
/// (<c>TempFileCleanupOptimization.RealTempRetentionMinutes</c>); el conteo
/// de pendientes y el borrado usan el mismo predicado
/// (<c>SafeFileEnumeration.IsOlderThan</c>), así que fijar el helper fija
/// Detect y Apply a la vez en cada clase.
/// </summary>
public sealed class TempRetentionTests : IDisposable
{
    private readonly string _dir;

    public TempRetentionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cao-retention-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string WriteFile(string name, int ageMinutes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-ageMinutes));
        return path;
    }

    [Theory]
    [InlineData(typeof(CleanupWindowsUpdateCache))]
    [InlineData(typeof(CleanupAppCaches))]
    [InlineData(typeof(CleanupGpuShaderCache))]
    [InlineData(typeof(CleanupNvidiaDownloaderCache))]
    public void GatedCleanups_ShareTenMinuteRetention(Type type)
    {
        var field = type.GetField("Retention",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy);
        Assert.NotNull(field);
        Assert.Equal(
            TimeSpan.FromMinutes(TempFileCleanupOptimization.RealTempRetentionMinutes),
            (TimeSpan)field.GetValue(null)!);
        Assert.Equal(TimeSpan.FromMinutes(10), (TimeSpan)field.GetValue(null)!);
    }

    [Fact]
    public void WindowsUpdateCache_CountIgnoresRecentFiles()
    {
        WriteFile("eleven.dat", 11);
        WriteFile("nine.dat", 9);

        Assert.Equal(1, CleanupWindowsUpdateCache.CountPendingFiles(_dir));
    }

    [Fact]
    public void AppCaches_CountAndDeleteIgnoreRecentFiles()
    {
        var sub = Path.Combine(_dir, "Cache");
        Directory.CreateDirectory(sub);
        var old = Path.Combine(sub, "eleven.dat");
        File.WriteAllText(old, "x");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddMinutes(-11));
        var fresh = Path.Combine(sub, "nine.dat");
        File.WriteAllText(fresh, "x");
        File.SetLastWriteTimeUtc(fresh, DateTime.UtcNow.AddMinutes(-9));

        // Vía conteo (la que alimenta a Detect): solo el de 11 min cuenta.
        Assert.Equal(1, CleanupAppCaches.CacheBytesFor(_dir, ["Cache"]));

        // Vía borrado (la que ejecuta Apply): solo el de 11 min cae.
        var (files, _) = CleanupAppCaches.DeleteCacheFiles(_dir, ["Cache"], CancellationToken.None);
        Assert.Equal(1, files);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void GpuShaderCache_CountIgnoresRecentFiles()
    {
        WriteFile("eleven.bin", 11);
        WriteFile("nine.bin", 9);

        Assert.Equal(1, CleanupGpuShaderCache.CacheBytesIn(_dir));
    }

    [Fact]
    public void NvidiaDownloader_PendingIgnoresRecentFiles()
    {
        WriteFile("eleven.exe", 11);
        WriteFile("nine.exe", 9);

        var (files, bytes) = CleanupNvidiaDownloaderCache.PendingIn(_dir);
        Assert.Equal(1, files);
        Assert.Equal(1, bytes);
    }

    [Theory]
    [InlineData(10, ">10 min")]
    [InlineData(10080, ">7d")]
    [InlineData(43200, ">30d")]
    public void FormatRetention_DaysOrMinutes(int minutes, string expected) =>
        Assert.Equal(expected, TempFileCleanupOptimization.FormatRetention(minutes));
}
