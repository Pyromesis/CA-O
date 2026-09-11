using System.Net.NetworkInformation;
using CAO.Core.Abstractions;
using CAO.Core.Catalog;
using CAO.Core.Optimization;
using CAO.Core.Optimizations.Gaming;
using CAO.Core.Optimizations.Network;
using CAO.Core.Optimizations.Storage;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Fase red/input/storage: Nagle por interfaz, Wi-Fi, ratón, MMCSS, defrag
/// solo-HDD y caché WU. Lógica pura + roundtrips con MemoryRegistry.
/// </summary>
public sealed class NewOptimizationsTests
{
    private const string Iface1 = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{11111111-2222-3333-4444-555555555555}";
    private const string Iface2 = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}";

    private static MemoryRegistry SeededNagle(bool iface1On, bool iface2On)
    {
        var registry = new MemoryRegistry();
        // Dummy para que la enumeración vea ambas interfaces (como el registro real).
        registry.SetValue(RegistryHive2.LocalMachine, Iface1, "DhcpIPAddress", "1.2.3.4", RegistryValueKind2.String);
        registry.SetValue(RegistryHive2.LocalMachine, Iface2, "DhcpIPAddress", "5.6.7.8", RegistryValueKind2.String);
        if (iface1On)
        {
            registry.SetValue(RegistryHive2.LocalMachine, Iface1, "TcpAckFrequency", 1, RegistryValueKind2.DWord);
            registry.SetValue(RegistryHive2.LocalMachine, Iface1, "TCPNoDelay", 1, RegistryValueKind2.DWord);
        }
        if (iface2On)
        {
            registry.SetValue(RegistryHive2.LocalMachine, Iface2, "TcpAckFrequency", 1, RegistryValueKind2.DWord);
            registry.SetValue(RegistryHive2.LocalMachine, Iface2, "TCPNoDelay", 1, RegistryValueKind2.DWord);
        }
        else
        {
            registry.SetValue(RegistryHive2.LocalMachine, Iface2, "TcpAckFrequency", 2, RegistryValueKind2.DWord);
        }
        return registry;
    }

    [Fact]
    public void NagleDetect_AllOnMixedAndEmpty()
    {
        var nagle = new DisableNagleTcpAcks();
        Assert.Equal(OptimizationState.AppliedByCao, nagle.Detect(SeededNagle(true, true)));
        Assert.Equal(OptimizationState.NotApplied, nagle.Detect(SeededNagle(true, false)));
        Assert.Equal(OptimizationState.NotApplied, nagle.Detect(SeededNagle(false, false)));
        Assert.Equal(OptimizationState.Unknown, nagle.Detect(new MemoryRegistry()));
    }

    [Fact]
    public async Task NagleApplyWritesBothValuesAndRevertRestores()
    {
        var nagle = new DisableNagleTcpAcks();
        var registry = SeededNagle(false, false);
        var snapshot = nagle.Capture(registry);
        var context = new OptimizationContext { Registry = registry };

        var applied = await nagle.ApplyAsync(context);
        Assert.True(applied.Success);
        Assert.Equal(OptimizationState.AppliedByCao, nagle.Detect(registry));

        var reverted = await nagle.RevertAsync(context, snapshot);
        Assert.True(reverted.Success);
        Assert.Equal(OptimizationState.NotApplied, nagle.Detect(registry));
        // La iface1 no tenía valores: el revert los borra, no deja 1.
        Assert.Null(registry.GetValue(RegistryHive2.LocalMachine, Iface1, "TcpAckFrequency"));
    }

    [Fact]
    public async Task NagleApplyWithoutInterfacesFailsHonestly()
    {
        var nagle = new DisableNagleTcpAcks();
        var context = new OptimizationContext { Registry = new MemoryRegistry() };
        var result = await nagle.ApplyAsync(context);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task NaglePreviewListsEveryInterface()
    {
        var nagle = new DisableNagleTcpAcks();
        var preview = await nagle.PreviewAsync(SeededNagle(true, false));
        Assert.Equal(2, preview.Lines.Count);
        Assert.All(preview.Lines, line => Assert.Equal("Registry", line.Kind));
    }

    [Theory]
    [InlineData("Wi-Fi", NetworkInterfaceType.Wireless80211, OperationalStatus.Up, "Wi-Fi")]
    [InlineData("Wi-Fi 2", NetworkInterfaceType.Wireless80211, OperationalStatus.Down, "Wi-Fi 2")]
    public void FindWifiInterface_PrefersUpWireless(string name, NetworkInterfaceType type, OperationalStatus status, string expected)
    {
        var nics = new[]
        {
            ("Ethernet", NetworkInterfaceType.Ethernet, OperationalStatus.Up),
            (name, type, status),
        };
        Assert.Equal(expected, DisableWifiBackgroundScan.FindWifiInterface(nics));
    }

    [Fact]
    public void FindWifiInterface_ReturnsNullWithoutWireless()
    {
        var nics = new[] { ("Ethernet", NetworkInterfaceType.Ethernet, OperationalStatus.Up) };
        Assert.Null(DisableWifiBackgroundScan.FindWifiInterface(nics));
        Assert.Null(DisableWifiBackgroundScan.FindWifiInterface(Array.Empty<(string, NetworkInterfaceType, OperationalStatus)>()));
    }

    [Fact]
    public void WifiDetectIsAlwaysUnknown()
    {
        Assert.Equal(OptimizationState.Unknown, new DisableWifiBackgroundScan().Detect(new MemoryRegistry()));
    }

    [Theory]
    [InlineData("3", DiskMedia.Hdd)]
    [InlineData("4", DiskMedia.Ssd)]
    [InlineData("5", DiskMedia.Scm)]
    [InlineData("0", DiskMedia.Unknown)]
    [InlineData("7", DiskMedia.Unknown)]
    public void MapMediaType_ClassifiesKnownValues(string raw, DiskMedia expected)
    {
        Assert.Equal(expected, DiskMediaDetector.MapMediaType(int.Parse(raw)));
        Assert.Equal(DiskMedia.Unknown, DiskMediaDetector.MapMediaType("texto"));
        Assert.Equal(DiskMedia.Unknown, DiskMediaDetector.MapMediaType(null));
    }

    [Fact]
    public void ResolveVolumeMedia_NeverThrowsAndReturnsValidEnum()
    {
        foreach (var volume in new[] { "C:", "Z:", "", "nope", "1:" })
        {
            var media = DiskMediaDetector.ResolveVolumeMedia(volume);
            Assert.True(Enum.IsDefined(media));
        }
    }

    [Theory]
    [InlineData("Total fragmented space              = 12%", 12.0)]
    [InlineData("Espacio total fragmentado             = 7 %", 7.0)]
    [InlineData("TOTAL FRAGMENTED SPACE = 0%", 0.0)]
    [InlineData("sin datos", null)]
    [InlineData("", null)]
    public void ParseFragmentationPercent_ReadsBothLanguages(string output, double? expected)
    {
        Assert.Equal(expected, DefragmentHddOnly.ParseFragmentationPercent(output));
    }

    [Fact]
    public void NewEntriesAreRegisteredWithCompleteMetadata()
    {
        foreach (var id in new[]
        {
            "disable-nagle-tcp-acks", "disable-wifi-background-scan",
            "disable-pointer-precision", "mouse-driver-queue-trim", "mmcss-system-responsiveness",
            "defragment-hdd-only", "cleanup-windows-update-cache", "disable-dynamic-tick",
        })
        {
            var optimization = OptimizationCatalog.All.First(o => o.Definition.Id == id);
            Assert.False(string.IsNullOrWhiteSpace(optimization.Definition.NameEs));
            Assert.False(string.IsNullOrWhiteSpace(optimization.Definition.TooltipEs));
        }
        Assert.Equal(89, OptimizationCatalog.All.Count);
    }

    [Theory]
    [InlineData("disable-nagle-tcp-acks", "disable-nagle-tcp-acks")]
    [InlineData("defragment-hdd-only", "defragment-hdd-only")]
    public async Task NewRegistryAndCustomEntriesExposePreview(string id, string expectedId)
    {
        var optimization = OptimizationCatalog.All.First(o => o.Definition.Id == id);
        var preview = await optimization.PreviewAsync(new MemoryRegistry());
        Assert.Equal(expectedId, preview.OptimizationId);
        Assert.NotEmpty(preview.Lines);
    }
}
