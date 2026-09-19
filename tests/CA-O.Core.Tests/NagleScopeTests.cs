using System.Net.NetworkInformation;
using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Gaming;
using CAO.Core.Optimizations.Network;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Task 5 (Plan 04, catálogo honesto): Nagle solo en interfaces físicas +
/// tooltips de tradeoff (MMCSS audio, cola de ratón eventos). Lógica pura +
/// roundtrips con MemoryRegistry y NICs sintéticas (hermético: sin BCL viva).
/// </summary>
public sealed class NagleScopeTests
{
    private const string PhysGuid = "{11111111-2222-3333-4444-555555555555}";
    private const string VirtGuid = "{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}";

    private static string KeyFor(string guid) => DisableNagleTcpAcks.InterfacesBase + "\\" + guid;

    private static MemoryRegistry SeededMixed()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.LocalMachine, KeyFor(PhysGuid), "DhcpIPAddress", "1.2.3.4", RegistryValueKind2.String);
        registry.SetValue(RegistryHive2.LocalMachine, KeyFor(VirtGuid), "DhcpIPAddress", "5.6.7.8", RegistryValueKind2.String);
        return registry;
    }

    private static IReadOnlyList<(string Id, string Name, string Description, NetworkInterfaceType Type, OperationalStatus Status)> MixedNics() =>
    [
        (PhysGuid, "Ethernet", "Intel Ethernet Controller", NetworkInterfaceType.Ethernet, OperationalStatus.Up),
        (VirtGuid, "VPN", "TAP-Windows Adapter", NetworkInterfaceType.Ethernet, OperationalStatus.Up),
    ];

    [Theory]
    // Físicas reales en Up → candidatas.
    [InlineData("Ethernet", "Intel Ethernet Controller", NetworkInterfaceType.Ethernet, OperationalStatus.Up, true)]
    [InlineData("Wi-Fi", "Intel Wireless-AC", NetworkInterfaceType.Wireless80211, OperationalStatus.Up, true)]
    [InlineData("Ethernet 2", "Realtek GbE Family", NetworkInterfaceType.GigabitEthernet, OperationalStatus.Up, true)]
    [InlineData("Ethernet 3", "Fast Ethernet Adapter", NetworkInterfaceType.FastEthernetT, OperationalStatus.Up, true)]
    [InlineData("Ethernet 4", "Fast Ethernet FX", NetworkInterfaceType.FastEthernetFx, OperationalStatus.Up, true)]
    // Tipos no físicos → fuera aunque estén Up.
    [InlineData("Loopback", "Software Loopback Interface", NetworkInterfaceType.Loopback, OperationalStatus.Up, false)]
    [InlineData("Teredo", "Teredo Tunneling Pseudo-Interface", NetworkInterfaceType.Tunnel, OperationalStatus.Up, false)]
    // Nombres/descripciones virtuales → fuera aunque el tipo sea Ethernet.
    [InlineData("VPN", "TAP-Windows Adapter", NetworkInterfaceType.Ethernet, OperationalStatus.Up, false)]
    [InlineData("vEthernet", "Hyper-V Virtual Ethernet Adapter", NetworkInterfaceType.Ethernet, OperationalStatus.Up, false)]
    [InlineData("vEthernet (WSL)", "WSL Hyper-V firewall", NetworkInterfaceType.Ethernet, OperationalStatus.Up, false)]
    [InlineData("VPN Client", "VPN Client Virtual Adapter", NetworkInterfaceType.Ethernet, OperationalStatus.Up, false)]
    [InlineData("VirtualBox", "VirtualBox Host-Only Adapter", NetworkInterfaceType.Ethernet, OperationalStatus.Up, false)]
    [InlineData("VMware", "VMware Virtual Ethernet Adapter", NetworkInterfaceType.Ethernet, OperationalStatus.Up, false)]
    // Caídas → fuera aunque sean físicas.
    [InlineData("Ethernet", "Intel Ethernet Controller", NetworkInterfaceType.Ethernet, OperationalStatus.Down, false)]
    [InlineData("Wi-Fi", "Intel Wireless-AC", NetworkInterfaceType.Wireless80211, OperationalStatus.Dormant, false)]
    public void IsPhysicalCandidate_ScopesToUpPhysicalNics(
        string name, string description, NetworkInterfaceType type, OperationalStatus status, bool expected)
    {
        Assert.Equal(expected, DisableNagleTcpAcks.IsPhysicalCandidate((name, description, type, status)));
    }

    [Fact]
    public async Task NagleApplyWithoutPhysicalCandidatesFailsHonestly()
    {
        var nagle = new DisableNagleTcpAcks();
        var registry = SeededMixed();
        var context = new OptimizationContext { Registry = registry };

        // Solo virtuales en la lista viva → Fail, nunca no-op silencioso.
        var virtualOnly = new[]
        {
            (VirtGuid, "VPN", "TAP-Windows Adapter", NetworkInterfaceType.Ethernet, OperationalStatus.Up),
        };
        var smiled = await nagle.ApplyAsync(context, virtualOnly);
        Assert.False(smiled.Success);

        // Nada tocado: ni siquiera la física del registro se escribe sin candidatas.
        Assert.Null(registry.GetValue(RegistryHive2.LocalMachine, KeyFor(PhysGuid), "TcpAckFrequency"));

        // Sin NICs en absoluto → Fail también.
        Assert.False((await nagle.ApplyAsync(context, [])).Success);

        // Registro vacío por la vía pública (BCL viva irrelevante: cero subclaves) → Fail.
        var empty = new OptimizationContext { Registry = new MemoryRegistry() };
        Assert.False((await nagle.ApplyAsync(empty)).Success);
    }

    [Fact]
    public async Task NagleApplyAndCaptureTouchOnlyPhysicalCandidates()
    {
        var nagle = new DisableNagleTcpAcks();
        var registry = SeededMixed();
        var nics = MixedNics();
        var context = new OptimizationContext { Registry = registry };

        var snapshot = nagle.Capture(registry, nics);
        Assert.NotEmpty(snapshot.Registry);
        Assert.All(snapshot.Registry, e => Assert.Contains(PhysGuid, e.KeyPath, StringComparison.OrdinalIgnoreCase));

        var applied = await nagle.ApplyAsync(context, nics);
        Assert.True(applied.Success);
        Assert.True(DisableNagleTcpAcks.HasNagleOff(registry, PhysGuid));
        Assert.Null(registry.GetValue(RegistryHive2.LocalMachine, KeyFor(VirtGuid), "TcpAckFrequency"));
        Assert.Null(registry.GetValue(RegistryHive2.LocalMachine, KeyFor(VirtGuid), "TCPNoDelay"));

        var reverted = await nagle.RevertAsync(context, snapshot);
        Assert.True(reverted.Success);
        Assert.Null(registry.GetValue(RegistryHive2.LocalMachine, KeyFor(PhysGuid), "TcpAckFrequency"));
    }

    [Fact]
    public async Task NagleMatchesRegistrySubkeyWithoutBraces()
    {
        // En producción las subclaves del registro vienen SIN llaves, mientras
        // que la BCL reporta los Ids CON llaves: la intersección solo cierra si
        // NormalizeId las quita. Sin esa normalización Apply tocaría cero NICs
        // (Fail perpetuo) mientras la suite seguiría en verde.
        const string bareGuid = "11111111-2222-3333-4444-555555555555";
        var nagle = new DisableNagleTcpAcks();
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.LocalMachine, KeyFor(bareGuid), "DhcpIPAddress", "1.2.3.4", RegistryValueKind2.String);
        var nics = new[]
        {
            (PhysGuid, "Ethernet", "Intel Ethernet Controller", NetworkInterfaceType.Ethernet, OperationalStatus.Up),
        };
        var context = new OptimizationContext { Registry = registry };

        Assert.Single(DisableNagleTcpAcks.PhysicalCandidateIds(registry, nics));

        var applied = await nagle.ApplyAsync(context, nics);
        Assert.True(applied.Success);
        Assert.True(DisableNagleTcpAcks.HasNagleOff(registry, bareGuid));
    }

    [Fact]
    public void GamingTooltipsDocumentTradeoffs()
    {
        Assert.Contains("audio", new MmcssSystemResponsiveness().Definition.TooltipEs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eventos", new MouseDriverQueueTrim().Definition.TooltipEs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hardware", new MouseDriverQueueTrim().Definition.TooltipEs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GamingTradeoffValuesStayUntuned()
    {
        // Solo texto en tooltips: los valores 10 y 32 no se tocan (prohibido aquí).
        var registry = new MemoryRegistry();
        var context = new OptimizationContext { Registry = registry };
        Assert.True((await new MmcssSystemResponsiveness().ApplyAsync(context)).Success);
        Assert.True((await new MouseDriverQueueTrim().ApplyAsync(context)).Success);
        Assert.Equal(10, Convert.ToInt32(registry.GetValue(
            RegistryHive2.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
            "SystemResponsiveness")));
        Assert.Equal(32, Convert.ToInt32(registry.GetValue(
            RegistryHive2.LocalMachine,
            @"SYSTEM\CurrentControlSet\Services\mouclass\Parameters",
            "MouseDataQueueSize")));
    }
}
