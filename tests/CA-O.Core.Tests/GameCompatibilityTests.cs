using CAO.Core.Gaming;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

public sealed class GameCompatibilityTests
{
    private static SystemContext CtxWithVanguard() => SystemContextFactory.Default() with { AntiCheats = new[] { new AntiCheatInfo(AntiCheatKind.Vanguard, "svc", new[] { "vgc" }) } };
    private static SystemContext CtxClean() => SystemContextFactory.Default();

    [Fact]
    public void VbsBloqueado_ConVanguard()
    {
        Assert.True(GameCompatibilityPolicy.IsBlocked("disable-vbs", CtxWithVanguard()));
        Assert.True(GameCompatibilityPolicy.IsBlocked("hypervisor-launchtype-off", CtxWithVanguard()));
        var (comp, _) = GameCompatibilityPolicy.Evaluate("disable-vbs", CtxWithVanguard());
        Assert.Equal(GameCompatibility.Blocked, comp);
    }

    [Fact]
    public void TransparenciaPermitida_ConVanguard()
    {
        var (comp, _) = GameCompatibilityPolicy.Evaluate("disable-transparency", CtxWithVanguard());
        Assert.Equal(GameCompatibility.Safe, comp);
        Assert.False(GameCompatibilityPolicy.IsBlocked("disable-transparency", CtxWithVanguard()));
    }

    [Fact]
    public void SinAntiCheat_TodoSafe()
    {
        Assert.False(GameCompatibilityPolicy.IsBlocked("disable-vbs", CtxClean()));
        Assert.Equal(GameCompatibility.Safe, GameCompatibilityPolicy.Evaluate("disable-transparency", CtxClean()).Compatibility);
    }

    [Fact]
    public void GetBlockedOptimizations_ConVanguard_ContieneVbs()
    {
        var blocked = GameCompatibilityPolicy.GetBlockedOptimizations(CtxWithVanguard());
        Assert.Contains("disable-vbs", blocked);
    }
}
