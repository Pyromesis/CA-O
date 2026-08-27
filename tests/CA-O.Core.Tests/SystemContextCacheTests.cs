using CAO.Infrastructure.SystemInterop;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>Cache TTL y thread-safety (§11).</summary>
public sealed class SystemContextCacheTests
{
    private static SystemContext Make(int build, string cpu) => new()
    {
        WindowsBuild = build,
        WindowsEdition = "Pro",
        Architecture = "X64",
        CpuName = cpu,
        CpuCores = 8,
        CpuLogicalProcessors = 16,
        RamGb = 32,
        HasSsd = true,
        IsLaptop = false,
        GpuName = "NVIDIA",
        GpuVendor = "NVIDIA",
        ThermalState = ThermalState.Nominal,
        MeasuredUtc = DateTime.UtcNow,
    };

    [Fact]
    public void Set_Then_TryGet_ReturnsSame()
    {
        var cache = new SystemContextCache();
        var ctx = Make(22631, "Intel");
        cache.Set(ctx);
        Assert.True(cache.TryGet(out var got));
        Assert.Equal("Intel", got!.CpuName);
    }

    [Fact]
    public void Invalidate_Clears()
    {
        var cache = new SystemContextCache();
        cache.Set(Make(22631, "Intel"));
        cache.Invalidate();
        Assert.False(cache.TryGet(out _));
        Assert.Equal(TimeSpan.MaxValue, cache.Age);
    }

    [Fact]
    public void StaticPart_Preserved_After_DynamicChange()
    {
        var cache = new SystemContextCache();
        var ctx1 = Make(22631, "Intel");
        cache.Set(ctx1);
        var ctx2 = Make(22631, "Intel") with { ThermalState = ThermalState.Warm };
        // static igual → TryGetStaleStatic debe seguir verdadero
        cache.Set(ctx2);
        Assert.True(cache.TryGetStaleStatic(out var stale));
        Assert.NotNull(stale);
    }
}
