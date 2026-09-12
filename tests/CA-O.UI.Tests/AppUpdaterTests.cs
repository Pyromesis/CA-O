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
    public void SelectFullPackageAsset_RejectsNonHttps()
    {
        var assets = new[]
        {
            ("CA-O-2.1.6-win-x64.zip", "http://example.com/full.zip", 2L),
        };

        Assert.Null(AppUpdater.SelectFullPackageAsset(assets));
    }

    [Theory]
    [InlineData("ABCDEF0123456789abcdef0123456789ABCDEF0123456789abcdef0123456789  CA-O-2.1.6-win-x64.zip\n", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789")]
    [InlineData("", null)]
    [InlineData("not-a-hash  file.zip", null)]
    [InlineData("xyz  file.zip", null)]
    [InlineData("abc", null)]
    public void TryParseSha256Sidecar_ParsesOrRejects(string content, string? expected)
    {
        Assert.Equal(expected, AppUpdater.TryParseSha256Sidecar(content));
    }

    [Fact]
    public void VerifyFileHash_MatchesAndRejectsTampered()
    {
        var path = Path.Combine(Path.GetTempPath(), "CA-O-Test-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
            var hex = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 1, 2, 3, 4 }));
            Assert.True(AppUpdater.VerifyFileHash(path, hex));
            Assert.True(AppUpdater.VerifyFileHash(path, hex.ToLowerInvariant()));
            Assert.False(AppUpdater.VerifyFileHash(path, new string('0', 64)));
            Assert.False(AppUpdater.VerifyFileHash(path, ""));
            File.WriteAllBytes(path, new byte[] { 9 });
            Assert.False(AppUpdater.VerifyFileHash(path, hex));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task DownloadAsync_AbortsOverCapAndDeletesPartial()
    {
        using var server = new LoopbackInfiniteServer();
        var dest = Path.Combine(Path.GetTempPath(), "CA-O-Test-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var ex = await Record.ExceptionAsync(() =>
                AppUpdater.DownloadAsync(server.Url, dest, progress: null, CancellationToken.None, maxBytes: 256 * 1024));
            Assert.IsType<InvalidOperationException>(ex);
            Assert.False(File.Exists(dest));
        }
        finally
        {
            try { File.Delete(dest); } catch { }
        }
    }

    [Fact]
    public async Task DownloadAsync_RejectsNonPositiveCap()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            AppUpdater.DownloadAsync("https://example.com/x.zip", "nul", null, CancellationToken.None, 0));
    }

    [Fact]
    public async Task ExtractWithProgressAsync_CapsEntriesAndBytes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CA-O-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var zipPath = Path.Combine(dir, "test.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                for (int i = 0; i < 3; i++)
                {
                    var entry = archive.CreateEntry($"file{i}.bin");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write(new string('A', 1000));
                }
            }
            // Tope de entradas: 3 entradas con tope 2 debe fallar.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AppUpdater.ExtractWithProgressAsync(zipPath, Path.Combine(dir, "out1"), null, CancellationToken.None, maxTotalBytes: long.MaxValue, maxEntries: 2));
            // Tope de bytes: ~3 KB con tope de 10 B debe fallar.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AppUpdater.ExtractWithProgressAsync(zipPath, Path.Combine(dir, "out2"), null, CancellationToken.None, maxTotalBytes: 10, maxEntries: 60000));
            // Sin topes restrictivos extrae bien (no regresión).
            await AppUpdater.ExtractWithProgressAsync(zipPath, Path.Combine(dir, "out3"), null, CancellationToken.None);
            Assert.Equal(3, Directory.GetFiles(Path.Combine(dir, "out3")).Length);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>Servidor HTTP loopback que sirve bytes infinitos sin Content-Length.</summary>
    private sealed class LoopbackInfiniteServer : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serve;

        public LoopbackInfiniteServer()
        {
            _listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((System.Net.IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/pkg.zip";
            _serve = Task.Run(async () =>
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    using var stream = client.GetStream();
                    var header = "HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n";
                    var headerBytes = System.Text.Encoding.ASCII.GetBytes(header);
                    await stream.WriteAsync(headerBytes, _cts.Token);
                    var chunk = new byte[65536];
                    while (!_cts.IsCancellationRequested)
                        await stream.WriteAsync(chunk, _cts.Token);
                }
                catch { }
            });
        }

        public string Url { get; }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            try { _serve.Wait(TimeSpan.FromSeconds(5)); } catch { }
            _cts.Dispose();
        }
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
