using CAO.Core.Abstractions;
using CAO.Core.Optimization;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Auditoría uno-por-uno: contradicciones y Detect-post-Apply.
/// - P1: planes de energía con Detect real (exclusión mutua: solo el activo aplica).
/// - G1: preferencia GPU con merge (no destruye tokens VRR/windowed).
/// - B1: StorageSense escribe en la key real (sin doble backslash).
/// </summary>
public sealed class ConflictAuditTests
{
    private const string SchemesKey = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
    private const string ActiveValue = "ActivePowerScheme";

    private static MemoryRegistry RegistryWithScheme(string guid)
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.LocalMachine, SchemesKey, ActiveValue, guid, RegistryValueKind2.String);
        return registry;
    }

    [Fact]
    public void MaximumPowerPlan_ActiveHigh_IsApplied()
    {
        var detect = new CAO.Core.Optimizations.Performance.MaximumPowerPlan()
            .Detect(RegistryWithScheme(PowerSchemes.HighPerformanceGuid));
        Assert.Equal(OptimizationState.AppliedByCao, detect);
    }

    [Fact]
    public void MaximumPowerPlan_ActiveUltimate_IsApplied_NotReappliable()
    {
        // Asimetría Detect/Verify corregida: Ultimate también cuenta.
        var detect = new CAO.Core.Optimizations.Performance.MaximumPowerPlan()
            .Detect(RegistryWithScheme(PowerSchemes.UltimatePerformanceGuid));
        Assert.Equal(OptimizationState.AppliedByCao, detect);
    }

    [Fact]
    public void PowerPlans_MutuallyExclusive_OnlyActiveIsApplied()
    {
        var registry = RegistryWithScheme(PowerSchemes.BalancedGuid);
        var high = new CAO.Core.Optimizations.Performance.MaximumPowerPlan().Detect(registry);
        var best = new CAO.Core.Optimizations.Power.SetBestPerformanceAc().Detect(registry);
        var balanced1 = new CAO.Core.Optimizations.Power.RestoreBalancedPowerDc().Detect(registry);
        var balanced2 = new CAO.Core.Optimizations.Power.RestorePowerPlanAfterGaming().Detect(registry);
        var ultimate = new CAO.Core.Optimizations.Gaming.ConfigureGamingPowerModeAc().Detect(registry);

        Assert.Equal(OptimizationState.NotApplied, high);
        Assert.Equal(OptimizationState.NotApplied, best);
        Assert.Equal(OptimizationState.NotApplied, ultimate);
        Assert.Equal(OptimizationState.AppliedByCao, balanced1);
        Assert.Equal(OptimizationState.AppliedByCao, balanced2);
    }

    [Fact]
    public void GamingPowerMode_ActiveUltimate_IsApplied()
    {
        var detect = new CAO.Core.Optimizations.Gaming.ConfigureGamingPowerModeAc()
            .Detect(RegistryWithScheme(PowerSchemes.UltimatePerformanceGuid));
        Assert.Equal(OptimizationState.AppliedByCao, detect);
    }

    [Fact]
    public void PowerScheme_Unknown_WhenUnreadable()
    {
        var detect = new CAO.Core.Optimizations.Power.SetBestPerformanceAc().Detect(new MemoryRegistry());
        Assert.Equal(OptimizationState.Unknown, detect);
    }

    [Fact]
    public void GpuPreference_Merge_PreservesOtherTokens()
    {
        var merged = CAO.Core.Optimizations.Gaming.SetGamesHighPerformanceGpu
            .UpdateGpuPreference("VRROptimizeEnable=1;SwapEffectUpgradeEnable=1;");
        Assert.Contains("GpuPreference=2", merged, StringComparison.Ordinal);
        Assert.Contains("VRROptimizeEnable=1", merged, StringComparison.Ordinal);
        Assert.Contains("SwapEffectUpgradeEnable=1", merged, StringComparison.Ordinal);

        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser,
            @"Software\Microsoft\DirectX\UserGpuPreferences",
            "DirectXUserGlobalSettings", merged, RegistryValueKind2.String);
        var detect = new CAO.Core.Optimizations.Gaming.SetGamesHighPerformanceGpu().Detect(registry);
        Assert.Equal(OptimizationState.AppliedByCao, detect);
    }

    [Fact]
    public async Task StorageSenseTempCleanup_WritesRealKey_DetectApplied()
    {
        var registry = new MemoryRegistry();
        var opt = new CAO.Core.Optimizations.Storage.StorageSenseTempCleanup();
        var context = new OptimizationContext { Registry = registry };
        var result = await opt.ApplyAsync(context);
        Assert.True(result.Success);
        Assert.Equal(OptimizationState.AppliedByCao, opt.Detect(registry));
        // Sin keys fantasma con doble backslash.
        Assert.DoesNotContain(registry.Store.Keys, k => k.Contains("software\\\\microsoft"));
        Assert.Contains(registry.Store.Keys, k => k.Contains(@"software\microsoft\windows\currentversion\storagesense"));
    }

    [Fact]
    public async Task StorageSenseRecycleBin_WritesRealKey_DetectApplied()
    {
        var registry = new MemoryRegistry();
        var opt = new CAO.Core.Optimizations.Storage.StorageSenseRecycleBinPolicy();
        var context = new OptimizationContext { Registry = registry };
        var result = await opt.ApplyAsync(context);
        Assert.True(result.Success);
        Assert.Equal(OptimizationState.AppliedByCao, opt.Detect(registry));
        Assert.DoesNotContain(registry.Store.Keys, k => k.Contains("software\\\\microsoft"));
    }
}
