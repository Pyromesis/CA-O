using CAO.Core.Optimizations.Storage;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>El fix %TEMP%-usuario: solo dirs existentes bajo \Users + humo de la base.</summary>
public sealed class UserTempDirsTests
{
    [Fact]
    public void ProfileSubDirs_ReturnsOnlyExistingDirsUnderUsers()
    {
        var dirs = TempFileCleanupOptimization.ProfileSubDirs("AppData", "Local", "Temp");
        foreach (var dir in dirs)
        {
            Assert.True(Path.IsPathRooted(dir));
            Assert.True(Directory.Exists(dir));
            Assert.Contains("Users", dir, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("AppData", "Local", "Temp"), dir, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CleanupWindowsTemp_TargetsKeepSystemAndServiceEntries()
    {
        var optimization = new CleanupWindowsTemp();
        // PreviewAsync ejercita Targets+ExpandDir sin borrar nada.
        var preview = await optimization.PreviewAsync(new MemoryRegistry(), CancellationToken.None);
        Assert.Contains(preview.Lines, l => l.Target.Contains("Temp", StringComparison.OrdinalIgnoreCase));
    }
}
