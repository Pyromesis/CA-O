using CAO.Core.Interfaces;

namespace CAO.Infrastructure.Networking;

public sealed class WmiDnsConfigurationProvider : IDnsConfigurationProvider
{
    public IReadOnlyList<DnsAdapterInfo> GetAdapters()
    {
        var list = new List<DnsAdapterInfo>();
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                var props = nic.GetIPProperties();
                var dnsV4 = props.DnsAddresses.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a)).Select(a => a.ToString()).ToList();
                var dnsV6 = props.DnsAddresses.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && !a.IsIPv6LinkLocal).Select(a => a.ToString()).ToList();
                var isVirtual = IsVirtualOrVpn(nic.Name);
                list.Add(new DnsAdapterInfo(nic.Name, nic.Id, nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up, isVirtual, dnsV4, dnsV6, false));
            }
        }
        catch { }
        return list;
    }

    public DnsAdapterInfo? GetAdapter(string interfaceName) => GetAdapters().FirstOrDefault(a => a.Name.Equals(interfaceName, StringComparison.OrdinalIgnoreCase));
    public IReadOnlyList<string> GetDnsServers(string interfaceName, bool ipv6 = false)
    {
        var a = GetAdapter(interfaceName);
        return a == null ? Array.Empty<string>() : ipv6 ? a.CurrentDnsV6 : a.CurrentDnsV4;
    }

    public bool IsVirtualOrVpn(string interfaceName)
    {
        var lower = interfaceName.ToLowerInvariant();
        return lower.Contains("hyper-v") || lower.Contains("virtual") || lower.Contains("vmware") || lower.Contains("virtualbox")
            || lower.Contains("vpn") || lower.Contains("tap") || lower.Contains("tun") || lower.Contains("wsl");
    }
}

public sealed class FakeDnsConfigurationProvider : IDnsConfigurationProvider
{
    private readonly Dictionary<string, DnsAdapterInfo> _adapters = new(StringComparer.OrdinalIgnoreCase);
    public FakeDnsConfigurationProvider(IEnumerable<DnsAdapterInfo>? initial = null)
    {
        if (initial != null) foreach (var a in initial) _adapters[a.Name] = a;
    }
    public void SetAdapter(DnsAdapterInfo info) => _adapters[info.Name] = info;
    public IReadOnlyList<DnsAdapterInfo> GetAdapters() => _adapters.Values.ToList();
    public DnsAdapterInfo? GetAdapter(string interfaceName) => _adapters.TryGetValue(interfaceName, out var v) ? v : null;
    public IReadOnlyList<string> GetDnsServers(string interfaceName, bool ipv6 = false)
    {
        var a = GetAdapter(interfaceName);
        return a == null ? Array.Empty<string>() : ipv6 ? a.CurrentDnsV6 : a.CurrentDnsV4;
    }
    public bool IsVirtualOrVpn(string interfaceName) => GetAdapter(interfaceName)?.IsVirtual ?? false;
    public void Apply(string interfaceName, string primary, string? secondary)
    {
        if (_adapters.TryGetValue(interfaceName, out var cur))
        {
            var list = new List<string> { primary };
            if (!string.IsNullOrWhiteSpace(secondary)) list.Add(secondary!);
            _adapters[interfaceName] = cur with { CurrentDnsV4 = list };
        }
    }
}
