using System.IO;

using CAO.Core.Optimizations.Storage;

using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// CAO-BUG-2026-10-06: la limpieza de caches de navegador recorria solo el perfil
/// "Default". Chrome y Edge numeran el resto de perfiles ("Profile 1", "Profile 2",
/// ...), de modo que el navegador que el usuario usa a diario no se limpiaba nunca.
/// Estos tests fijan que se cubren TODOS los perfiles y que las carpetas propias del
/// navegador (Crashpad, GraphiteDawnCache...) no se tratan como perfiles.
/// </summary>
public sealed class BrowserProfileCacheTargetsTests : IDisposable
{
    private readonly string _userData =
        Path.Combine(Path.GetTempPath(), $"cao-userdata-{Guid.NewGuid():N}");

    public BrowserProfileCacheTargetsTests()
    {
        Directory.CreateDirectory(Path.Combine(_userData, "Default", "Cache"));
        Directory.CreateDirectory(Path.Combine(_userData, "Profile 1", "Code Cache"));
        Directory.CreateDirectory(Path.Combine(_userData, "Profile 2", "GPUCache"));
        Directory.CreateDirectory(Path.Combine(_userData, "Profile 3", "ShaderCache"));
        // Carpetas del propio navegador que NO son perfiles.
        Directory.CreateDirectory(Path.Combine(_userData, "Crashpad", "reports"));
        Directory.CreateDirectory(Path.Combine(_userData, "GraphiteDawnCache"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_userData, recursive: true); }
        catch { /* el temporales se limpia solo */ }
    }

    [Fact]
    public void CoversEveryProfile_NotOnlyDefault()
    {
        var targets = CleanupBrowserCodeCache.CacheTargetsUnder(_userData);

        Assert.Equal(4, targets.Count);
        Assert.Contains(targets, t => t.Directory == Path.Combine(_userData, "Default", "Cache"));
        Assert.Contains(targets, t => t.Directory == Path.Combine(_userData, "Profile 1", "Code Cache"));
        Assert.Contains(targets, t => t.Directory == Path.Combine(_userData, "Profile 2", "GPUCache"));
        Assert.Contains(targets, t => t.Directory == Path.Combine(_userData, "Profile 3", "ShaderCache"));
    }

    [Fact]
    public void IgnoresDirectoriesThatAreNotBrowserProfiles()
    {
        var targets = CleanupBrowserCodeCache.CacheTargetsUnder(_userData);

        Assert.DoesNotContain(targets, t => t.Directory.Contains("Crashpad", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(targets, t => t.Directory.Contains("GraphiteDawnCache", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OnlyTargetsRegenerableCachesOlderThanOneDay()
    {
        foreach (var target in CleanupBrowserCodeCache.CacheTargetsUnder(_userData))
        {
            Assert.Equal("*.*", target.Pattern);
            Assert.Equal(1, target.OlderThanDays);
        }
    }
}