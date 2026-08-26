using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>
/// Responsible cache for SystemContext (Fase 48): TTL + invalidation + version + source.
/// Static fingerprint (hardware) cached 24h, dynamic state (thermal, battery, pendingReboot) 30s.
/// </summary>
public sealed class SystemContextCache
{
    private readonly object _lock = new();
    private SystemContext? _cached;
    private DateTime _cachedAt;
    private const int StaticTtlMinutes = 60 * 24;
    private static readonly TimeSpan DynamicTtl = TimeSpan.FromSeconds(30);

    private static bool IsStaticEqual(SystemContext a, SystemContext b) =>
        a.WindowsBuild == b.WindowsBuild && a.WindowsEdition == b.WindowsEdition &&
        a.Architecture == b.Architecture && a.CpuName == b.CpuName &&
        a.CpuCores == b.CpuCores && a.CpuLogicalProcessors == b.CpuLogicalProcessors &&
        a.RamGb == b.RamGb && a.HasSsd == b.HasSsd && a.IsLaptop == b.IsLaptop &&
        a.GpuName == b.GpuName && a.GpuVendor == b.GpuVendor;

    public bool TryGet(out SystemContext? context)
    {
        lock (_lock)
        {
            if (_cached is null) { context = null; return false; }
            var age = DateTime.UtcNow - _cachedAt;
            if (age > TimeSpan.FromMinutes(StaticTtlMinutes)) { context = null; return false; }
            // Dynamic parts expire faster: if any dynamic field is stale, treat as miss
            if (age > DynamicTtl)
            {
                // Allow stale static via separate path? For now, full miss after DynamicTtl to refresh thermals/battery.
                context = null; return false;
            }
            context = _cached;
            return true;
        }
    }

    public void Set(SystemContext context)
    {
        lock (_lock)
        {
            // Preserve hardware fingerprint if only dynamic changed and static equal? We keep full context but could merge.
            _cached = context;
            _cachedAt = DateTime.UtcNow;
        }
    }

    public void Invalidate()
    {
        lock (_lock)
        {
            _cached = null;
        }
    }

    public TimeSpan Age
    {
        get { lock (_lock) { return _cached is null ? TimeSpan.MaxValue : DateTime.UtcNow - _cachedAt; } }
    }

    public string Source => _cached is null ? "none" : "WMI+Security+Thermal+Games+PendingReboot";
    public string Version => "1";
}
