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
