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

    private SystemContext? _staticPart;
    private DateTime _staticAt;

    public bool TryGet(out SystemContext? context)
    {
        lock (_lock)
        {
            if (_cached is null) { context = null; return false; }
            var age = DateTime.UtcNow - _cachedAt;
            var staticAge = DateTime.UtcNow - _staticAt;
            if (staticAge > TimeSpan.FromMinutes(StaticTtlMinutes)) { context = null; return false; }
            if (age > DynamicTtl)
            {
                // Dynamic expirado → devolver parte estática con marca stale para refresh no bloqueante (§11)
                context = _cached;
                return false; // señal: necesita refresco dinámico pero puede usar stale si no hay tiempo
            }
            context = _cached;
            return true;
        }
    }

    public bool TryGetStaleStatic(out SystemContext? context)
    {
        lock (_lock)
        {
            if (_staticPart is null) { context = null; return false; }
            if (DateTime.UtcNow - _staticAt > TimeSpan.FromMinutes(StaticTtlMinutes)) { context = null; return false; }
            context = _staticPart;
            return true;
        }
    }

    public void Set(SystemContext context)
    {
        lock (_lock)
        {
            var isFirst = _staticPart is null;
            var staticChanged = _staticPart is null || !IsStaticEqual(_staticPart, context);
            _cached = context;
            _cachedAt = DateTime.UtcNow;
            if (isFirst || staticChanged)
            {
                _staticPart = context;
                _staticAt = DateTime.UtcNow;
            }
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
