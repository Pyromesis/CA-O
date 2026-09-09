using CAO.UI.Helpers;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>Lógica pura del auto-update: versiones y selección de paquete.</summary>
public sealed class AppUpdaterTests
{
    [Theory]
    [InlineData("2.1.5", "v2.1.6", true)]
    [InlineData("2.1.5", "2.1.6", true)]
    [InlineData("2.1.5", "v2.1.5", false)]
    [InlineData("2.1.6", "v2.1.5", false)]
    [InlineData("2.1.5", null, false)]
    [InlineData("2.1.5", "", false)]
    [InlineData("2.1.5", "latest", false)]
    [InlineData("2.1.5", "v2.2.0", true)]
    public void IsNewer_ComparesVersionsCorrectly(string current, string? tag, bool expected)
    {
        Assert.Equal(expected, AppUpdater.IsNewer(current, tag));
    }

    [Fact]
    public void SelectFullPackageAsset_PrefersFullZipOverGuiOnline()
    {
        var assets = new[]
        {
            ("CA-O-Setup-GUI-x64.zip", "https://example.com/gui.zip", 1L),
            ("CA-O-2.1.6-win-x64.zip", "https://example.com/full.zip", 2L),
        };

        var picked = AppUpdater.SelectFullPackageAsset(assets);

        Assert.NotNull(picked);
        Assert.Equal("https://example.com/full.zip", picked.Value.Url);
    }

    [Fact]
    public void SelectFullPackageAsset_ReturnsNullWhenOnlyGui()
    {
        var assets = new[]
        {
            ("CA-O-Setup-GUI-x64.zip", "https://example.com/gui.zip", 1L),
        };

        Assert.Null(AppUpdater.SelectFullPackageAsset(assets));
    }

    [Fact]
    public async Task ExecuteWithRetry_RetriesSharingViolationThenSucceeds()
    {
        var attempts = 0;

        await AppUpdater.ExecuteWithRetryAsync(() =>
        {
            attempts++;
            if (attempts < 3)
                throw new IOException("being used by another process");
            return Task.CompletedTask;
        }, maxAttempts: 5, baseDelayMs: 1);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteWithRetry_DoesNotRetryOtherErrors()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AppUpdater.ExecuteWithRetryAsync(() =>
            {
                attempts++;
                throw new InvalidOperationException("boom");
            }, maxAttempts: 5, baseDelayMs: 1));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExtractWithProgress_ExtractsFilesAndReportsCompletion()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cao-extract-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var zipPath = Path.Combine(dir, "pack.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (var name in new[] { "a.txt", "sub/b.txt", "sub/c.txt" })
                {
                    var entry = archive.CreateEntry(name);
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write("data-" + name);
                }
            }
            var dest = Path.Combine(dir, "out");
            var reports = new List<(double Ratio, int Done, int Total)>();
            var progress = new Progress<(double Ratio, int Done, int Total)>(p => reports.Add(p));
            await AppUpdater.ExtractWithProgressAsync(zipPath, dest, progress, CancellationToken.None);
            Assert.Equal("data-a.txt", File.ReadAllText(Path.Combine(dest, "a.txt")));
            Assert.Equal("data-sub/b.txt", File.ReadAllText(Path.Combine(dest, "sub", "b.txt")));
            Assert.NotEmpty(reports);
            Assert.Equal(1.0, reports[^1].Ratio);
            Assert.Equal(3, reports[^1].Total);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public async Task ExtractWithProgress_RejectsZipSlip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cao-slip-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var zipPath = Path.Combine(dir, "evil.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../escape.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("evil");
            }
            await Assert.ThrowsAsync<IOException>(() =>
                AppUpdater.ExtractWithProgressAsync(zipPath, Path.Combine(dir, "out"), null, CancellationToken.None));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void UnblockTree_RemovesZoneIdentifierKeepsContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cao-unblock-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "app.exe");
            File.WriteAllText(file, "binary-stub");
            File.WriteAllText(file + ":Zone.Identifier", "[ZoneTransfer]\nZoneId=3");
            Assert.True(AdsExists(file));
            AppUpdater.UnblockTree(dir);
            Assert.False(AdsExists(file));
            Assert.Equal("binary-stub", File.ReadAllText(file));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void UnblockTree_MissingDirDoesNotThrow()
    {
        Assert.Equal(0, AppUpdater.UnblockTree(Path.Combine(Path.GetTempPath(), "cao-nope-" + Guid.NewGuid().ToString("N"))));
    }

    private static bool AdsExists(string file)
    {
        try
        {
            using var _ = File.OpenRead(file + ":Zone.Identifier");
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task ExecuteWithRetry_GivesUpAfterMaxAttempts()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<IOException>(() =>
            AppUpdater.ExecuteWithRetryAsync(() =>
            {
                attempts++;
                throw new IOException("locked");
            }, maxAttempts: 3, baseDelayMs: 1));

        Assert.Equal(3, attempts);
    }
}
