namespace CAO.Core.Interfaces;

/// <summary>Abstraction for DNS read/apply/verify to enable testing without real adapters.</summary>
public interface IDnsConfigurationProvider
{
    IReadOnlyList<DnsAdapterInfo> GetAdapters();
    DnsAdapterInfo? GetAdapter(string interfaceName);
    /// <summary>Returns current DNS servers for adapter (IPv4 first).</summary>
    IReadOnlyList<string> GetDnsServers(string interfaceName, bool ipv6 = false);
    bool IsVirtualOrVpn(string interfaceName);
}

public sealed record DnsAdapterInfo(string Name, string Id, bool IsUp, bool IsVirtual, IReadOnlyList<string> CurrentDnsV4, IReadOnlyList<string> CurrentDnsV6, bool DhcpEnabled);

public sealed record DnsApplyRequest(string InterfaceName, string PrimaryDns, string? SecondaryDns, bool UseIpv6 = false);
public sealed record DnsApplyResult(bool Success, string MessageEs, string? ErrorCode, DnsAdapterInfo? Before, DnsAdapterInfo? After);
