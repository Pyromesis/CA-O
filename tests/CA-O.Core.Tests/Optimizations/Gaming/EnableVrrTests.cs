using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Gaming;
using CAO.Core.Tests;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests.Optimizations.Gaming;

/// <summary>
/// Tests for EnableVrr (Optimization #21).
/// Real Windows implementation using HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings.
/// Shares the same registry value as EnableWindowedGameOptimizations - both settings are preserved.
/// </summary>
public sealed class EnableVrrTests
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ValueName = "DirectXUserGlobalSettings";
    private const string VrrEnabled = "VRROptimizeEnable=1;";
    private const string VrrDisabled = "VRROptimizeEnable=0;";
    private const string WindowedEnabled = "SwapEffectUpgradeEnable=1;";
    private const string WindowedDisabled = "SwapEffectUpgradeEnable=0;";

    [Fact]
    public void Detect_WhenNotApplied_ReturnsNotApplied()
    {
        var registry = new MemoryRegistry();
        var optimization = new EnableVrr();

        var state = optimization.Detect(registry);

        Assert.Equal(OptimizationState.NotApplied, state);
    }

    [Fact]
    public void Detect_WhenApplied_ReturnsAppliedByCao()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, VrrEnabled, RegistryValueKind2.String);
        var optimization = new EnableVrr();

        var state = optimization.Detect(registry);

        Assert.Equal(OptimizationState.AppliedByCao, state);
    }

    [Fact]
    public void Detect_WhenDisabledValue_ReturnsNotApplied()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, VrrDisabled, RegistryValueKind2.String);
        var optimization = new EnableVrr();

        var state = optimization.Detect(registry);

        Assert.Equal(OptimizationState.NotApplied, state);
    }

    [Fact]
    public void Detect_WithVariations_HandlesCaseAndSemicolon()
    {
        var testValues = new[]
        {
            "VRROptimizeEnable=1",
            "VRROptimizeEnable=1;",
            "vrroptimizeenable=1;",
            "VRROptimizeEnable = 1 ;",
        };

        foreach (var value in testValues)
        {
            var registry = new MemoryRegistry();
            registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, value, RegistryValueKind2.String);
            var optimization = new EnableVrr();

            var state = optimization.Detect(registry);
            Assert.Equal(OptimizationState.AppliedByCao, state);
        }
    }

    [Fact]
    public void Capture_WhenValueExists_CapturesExactValue()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, VrrDisabled, RegistryValueKind2.String);
        var optimization = new EnableVrr();

        var snapshot = optimization.Capture(registry);

        Assert.Single(snapshot.Registry);
        var entry = snapshot.Registry[0];
        Assert.Equal(RegistryHive2.CurrentUser.ToString(), entry.Hive);
        Assert.Equal(KeyPath, entry.KeyPath);
        Assert.Equal(ValueName, entry.ValueName);
        Assert.Equal(VrrDisabled, entry.Value);
        Assert.True(entry.Existed);
        Assert.Equal(RegistryValueKind2.String, entry.Kind);
    }

    [Fact]
    public void Capture_WhenValueDoesNotExist_CapturesNonExistence()
    {
        var registry = new MemoryRegistry();
        var optimization = new EnableVrr();

        var snapshot = optimization.Capture(registry);

        Assert.Single(snapshot.Registry);
        var entry = snapshot.Registry[0];
        Assert.False(entry.Existed);
        Assert.Null(entry.Value);
    }

    [Fact]
    public async Task ApplyAsync_WritesVrrEnabledValue()
    {
        var registry = new MemoryRegistry();
        var context = new OptimizationContext { Registry = registry };
        var optimization = new EnableVrr();

        var result = await optimization.ApplyAsync(context);

        Assert.True(result.Success);
        var written = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        Assert.Equal(VrrEnabled, written);
    }

    [Fact]
    public async Task ApplyAsync_PreservesExistingWindowedGameOptimization()
    {
        var registry = new MemoryRegistry();
        // Pre-existing windowed game optimization setting
        registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, WindowedEnabled, RegistryValueKind2.String);
        var context = new OptimizationContext { Registry = registry };
        var optimization = new EnableVrr();

        await optimization.ApplyAsync(context);

        var written = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName) as string;
        Assert.NotNull(written);
        Assert.Contains("SwapEffectUpgradeEnable=1", written);
        Assert.Contains("VRROptimizeEnable=1", written);
    }

    [Fact]
    public async Task ApplyAsync_IsIdempotent()
    {
        var registry = new MemoryRegistry();
        var context = new OptimizationContext { Registry = registry };
        var optimization = new EnableVrr();

        await optimization.ApplyAsync(context);
        var firstResult = await optimization.ApplyAsync(context);

        Assert.True(firstResult.Success);
        var written = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        Assert.Equal(VrrEnabled, written);
    }

    [Fact]
    public async Task RevertAsync_WhenValueExisted_RestoresOriginalValue()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, VrrDisabled, RegistryValueKind2.String);
        var context = new OptimizationContext { Registry = registry };
        var optimization = new EnableVrr();

        var snapshot = optimization.Capture(registry);
        await optimization.ApplyAsync(context);
        var revertResult = await optimization.RevertAsync(context, snapshot);

        Assert.True(revertResult.Success);
        var restored = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        Assert.Equal(VrrDisabled, restored);
    }

    [Fact]
    public async Task RevertAsync_WhenValueDidNotExist_DeletesValue()
    {
        var registry = new MemoryRegistry();
        var context = new OptimizationContext { Registry = registry };
        var optimization = new EnableVrr();

        var snapshot = optimization.Capture(registry);
        await optimization.ApplyAsync(context);
        var revertResult = await optimization.RevertAsync(context, snapshot);

        Assert.True(revertResult.Success);
        var restored = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        Assert.Null(restored);
    }

    [Fact]
    public async Task FullCycle_ApplyThenVerifyThenRollbackThenVerify()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, VrrDisabled, RegistryValueKind2.String);
        var context = new OptimizationContext { Registry = registry };
        var optimization = new EnableVrr();

        // Capture original state
        var snapshot = optimization.Capture(registry);
        Assert.Equal(VrrDisabled, registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName));

        // Apply
        var applyResult = await optimization.ApplyAsync(context);
        Assert.True(applyResult.Success);
        Assert.Equal(VrrEnabled, registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName));

        // Verify
        var verifyState = optimization.Detect(registry);
        Assert.Equal(OptimizationState.AppliedByCao, verifyState);

        // Rollback
        var revertResult = await optimization.RevertAsync(context, snapshot);
        Assert.True(revertResult.Success);
        Assert.Equal(VrrDisabled, registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName));

        // Verify rollback
        var verifyRollbackState = optimization.Detect(registry);
        Assert.Equal(OptimizationState.NotApplied, verifyRollbackState);
    }

    [Fact]
    public void Definition_HasCorrectMetadata()
    {
        var optimization = new EnableVrr();
        var def = optimization.Definition;

        Assert.Equal("enable-vrr", def.Id);
        Assert.Equal(OptimizationCategory.Gaming, def.Category);
        Assert.Equal(PerformanceImpact.WorkloadDependent, def.ExpectedImpact);
        Assert.Equal(EvidenceLevel.Official, def.Evidence);
        Assert.Equal(RiskLevel.Low, def.Risk);
        Assert.Equal(SecurityImpact.None, def.SecurityImpact);
        Assert.Equal(CompatibilityStatus.Conditional, def.Compatibility);
        Assert.False(def.RequiresRestart);
        Assert.True(def.Reversible);
    }
}