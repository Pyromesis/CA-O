using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Optimization;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Catálogo de Microsoft: protocolo puro con HTML enlatado (forma real del
/// Search.aspx) + validación de entrada del engine. Sin red en tests.
/// </summary>
public sealed class CatalogDriverTests
{
    private const string SampleHtml = """
        <table><tr><td>
        <a id='aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee_link' href= "javascript:void(0);" onclick='goToDetails("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");' class="contentTextItemSpacerNoBreakLink">
        Old Thing Driver Update (1.0.0.1)</a>
        </td></tr><tr><td>
        <a id='30fe5516-a254-49c5-aaad-5643e00d0da6_link' href= "javascript:void(0);" onclick='goToDetails("30fe5516-a254-49c5-aaad-5643e00d0da6");' class="contentTextItemSpacerNoBreakLink">
        Intel Smart Sound Driver Update (2.3.20306.4)</a>
        </td></tr></table>
        """;

    private const string SampleDialog = """
        downloadInformation[0].files[0].url = 'https://catalog.s.download.windowsupdate.com/d/msdownload/update/driver/drvs/2024/10/x.cab';
        downloadInformation[0].files[1].url = 'http://evil.example.com/x.cab';
        """;

    private sealed class NoRestorePoints : IRestorePointService
    {
        public Task<(bool Success, string ReasonEs)> CreateAsync(string description, CancellationToken ct = default) =>
            Task.FromResult((false, "tests"));

        public Task<IReadOnlyList<CAO.Shared.RestorePointInfo>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CAO.Shared.RestorePointInfo>>(Array.Empty<CAO.Shared.RestorePointInfo>());
    }

    private static OptimizationEngine Engine() => new(
        new MemoryRegistry(), new NoRestorePoints(), new MemorySnapshotStore(), new MemoryHistory());

    [Fact]
    public void ParseOffers_ExtractsIdTitleVersionNewestFirst()
    {
        var offers = CatalogDriverProtocol.ParseOffers(SampleHtml);

        Assert.Equal(2, offers.Count);
        Assert.Equal("30fe5516-a254-49c5-aaad-5643e00d0da6", offers[0].UpdateId);
        Assert.Equal("Intel Smart Sound Driver Update (2.3.20306.4)", offers[0].Title);
        Assert.Equal("2.3.20306.4", offers[0].Version);
        Assert.Equal("1.0.0.1", offers[1].Version);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("<html>sin resultados</html>", 0)]
    public void ParseOffers_EmptyOnNoResults(string html, int expected)
    {
        Assert.Equal(expected, CatalogDriverProtocol.ParseOffers(html).Count);
    }

    [Fact]
    public void ParseDownloadUrls_KeepsOnlyMicrosoftHosts()
    {
        var urls = CatalogDriverProtocol.ParseDownloadUrls(SampleDialog);

        Assert.Single(urls);
        Assert.StartsWith("https://catalog.s.download.windowsupdate.com/", urls[0]);
        Assert.EndsWith(".cab", urls[0]);
    }

    [Fact]
    public void PickCabUrl_PrefersCab()
    {
        var urls = new[]
        {
            "https://download.windowsupdate.com/d/x.exe",
            "https://catalog.s.download.windowsupdate.com/d/y.cab",
        };
        Assert.EndsWith(".cab", CatalogDriverProtocol.PickCabUrl(urls));
        Assert.Null(CatalogDriverProtocol.PickCabUrl(Array.Empty<string>()));
    }

    [Fact]
    public void BuildQueries_FallsBackFromFullHwId()
    {
        var queries = CatalogDriverProtocol.BuildQueries(
            @"INTELAUDIO\CTLR_DEV_54C8&LINKTYPE_06&DEVTYPE_06&VEN_8086&DEV_AE50&SUBSYS_60071E50&REV_0001",
            "Intel Smart Sound Technology for USB Audio");

        Assert.True(queries.Count >= 3);
        Assert.StartsWith("INTELAUDIO\\", queries[0]);
        Assert.Equal("VEN_8086&DEV_AE50", queries[1]);
        Assert.Contains("Intel", queries[2]);
    }

    [Theory]
    [InlineData("2.3.20306.4", "2.3.20306.4")]
    [InlineData("13.4709", "13.4709")]
    [InlineData("sin-version", "")]
    public void ExtractVersion_ReadsTrailingParenthetical(string title, string expected)
    {
        var full = expected.Length == 0 ? title : $"{title} ({expected})";
        Assert.Equal(expected, CatalogDriverProtocol.ExtractVersion(full));
    }

    [Fact]
    public void ExtractVersion_ReadsDashSuffixedVersion()
    {
        Assert.Equal(
            "10.29.0.11472",
            CatalogDriverProtocol.ExtractVersion("Intel(R) Corporation - MEDIA - 10.29.0.11472"));
    }

    [Theory]
    [InlineData(@"PCI\VEN_8086&DEV_AE50&SUBSYS_60071E50&REV_31\3&11583659&0&20", "VEN_8086&DEV_AE50")]
    [InlineData(@"HID\VID_3151&PID_4026\7&2D0E7D8C&0&0001", "")]
    [InlineData("", "")]
    public void ExtractVenDev_ReadsFragmentOrEmpty(string hwid, string expected)
    {
        Assert.Equal(expected, CatalogDriverProtocol.ExtractVenDev(hwid));
    }

    [Theory]
    [InlineData("10.0.22621.1", "10.0.22621.1", false)]
    [InlineData("10.0.22621.1", "10.0.22621.2", true)]
    [InlineData("10.0.26100.1", "2.3.20306.4", true)]
    [InlineData("2.3.20306.4", "10.0.26100.1", true)]
    [InlineData("raro", "2.3.1", true)]
    [InlineData("1.0", "raro", true)]
    public void ShouldTryOffer_SkipsOnlySameVersion(string installed, string offered, bool expected)
    {
        Assert.Equal(expected, CatalogDriverProtocol.ShouldTryOffer(installed, offered));
    }

    [Fact]
    public void PickBestInfMatch_PrefersHardwareIdOverVenDev()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CA-O-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const string hwid = @"PCI\VEN_8086&DEV_AE50&SUBSYS_60071E50&REV_31\3&11583659&0&20";
            var generic = Path.Combine(dir, "generic.inf");
            var sibling = Path.Combine(dir, "sibling.inf");
            var exact = Path.Combine(dir, "exact.inf");
            File.WriteAllText(generic, "[Models]\n%Dev% = Install, PCI\\VEN_8086&DEV_AE51\n");
            File.WriteAllText(sibling, "[Models]\n%Dev% = Install, PCI\\VEN_8086&DEV_AE50&SUBSYS_99990000\n");
            File.WriteAllText(exact, "[Models]\n%Dev% = Install, " + hwid + "\n");
            Assert.Equal(exact, CatalogDriverProtocol.PickBestInfMatch(new[] { generic, sibling, exact }, hwid));
            Assert.Equal(sibling, CatalogDriverProtocol.PickBestInfMatch(new[] { generic, sibling }, hwid));
            Assert.Null(CatalogDriverProtocol.PickBestInfMatch(new[] { generic }, hwid));
            Assert.Null(CatalogDriverProtocol.PickBestInfMatch(Array.Empty<string>(), hwid));
            Assert.Null(CatalogDriverProtocol.PickBestInfMatch(new[] { exact }, ""));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Theory]
    [InlineData("x;calc")]
    [InlineData("")]
    public async Task SearchInvalidHardwareIdFailsWithoutNetwork(string hwid)
    {
        var result = await Engine().SearchCatalogDriversAsync(hwid, string.Empty);
        Assert.False(result.Success);
        Assert.Equal("invalid-hwid", result.Error);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public async Task DownloadInvalidUpdateIdFailsWithoutElevation(string updateId)
    {
        var result = await Engine().DownloadCatalogDriverAsync(updateId);
        Assert.False(result.Success);
        Assert.Equal("invalid-id", result.Error);
    }
}
