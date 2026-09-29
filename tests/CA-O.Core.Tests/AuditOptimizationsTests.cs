using CAO.Core.Abstractions;
using CAO.Core.Catalog;
using CAO.Core.Interfaces;
using CAO.Core.Optimizations.Gaming;
using CAO.Core.Optimizations.Network;
using CAO.Core.Optimizations.Performance;
using CAO.Core.Optimizations.Storage;
using CAO.Core.Optimizations.Troubleshoot;
using CAO.Shared;
using CAO.Shared.Security;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Auditoría HOLA-alpha 2026-09-29 (aprobada por operador): eliminaciones,
/// fusiones, bugs y 8 optimizaciones nuevas. Cada bloque con su prueba.
/// </summary>
public sealed class AuditOptimizationsTests
{
    private sealed class ScriptExecutor(Func<SystemCommandKey, IReadOnlyList<string>, PrivilegedCommandResult> script)
        : IPrivilegedCommandExecutor
    {
        public List<SystemCommandKey> Calls { get; } = [];

        public Task<PrivilegedCommandResult> ExecuteAsync(SystemCommandKey key, IReadOnlyList<string> arguments, CancellationToken ct = default)
        {
            Calls.Add(key);
            return Task.FromResult(script(key, arguments));
        }
    }

    private sealed class FakeServices : IServiceManager
    {
        public List<string> Calls { get; } = [];
        public string StartType { get; set; } = "Automatic";
        public bool ExistsResult { get; set; } = true;

        public string? GetStartType(string serviceName) => StartType;
        public void SetStartType(string serviceName, string startType) => Calls.Add("set:" + serviceName);
        public Task StopAsync(string serviceName, CancellationToken ct = default)
        {
            Calls.Add("stop:" + serviceName);
            return Task.CompletedTask;
        }
        public Task StartAsync(string serviceName, CancellationToken ct = default)
        {
            Calls.Add("start:" + serviceName);
            return Task.CompletedTask;
        }
        public bool Exists(string serviceName) => ExistsResult;
    }

    private static PrivilegedCommandResult Ok(string stdout = "") =>
        new(0, stdout, string.Empty, TimedOut: false);

    private static PrivilegedCommandResult Fail(string stderr = "error") =>
        new(1, string.Empty, stderr, TimedOut: false);

    // ---- BLOQUE A: eliminadas sin equivalente ----

    [Theory]
    [InlineData("disable-dynamic-tick")]
    [InlineData("disable-cortana")]
    [InlineData("gaming-display-refresh-rate-audit")]
    public void RetiredWithoutEquivalent_StaysOutOfProductionButTraceable(string retiredId)
    {
        Assert.Contains(retiredId, OptimizationCatalog.LegacyIds);
        Assert.DoesNotContain(OptimizationCatalog.All,
            o => o.Definition.Id.Equals(retiredId, StringComparison.Ordinal));
        Assert.Contains(OptimizationCatalog.AllLegacy,
            o => o.Definition.Id.Equals(retiredId, StringComparison.Ordinal));
        // Sin alias falso: resuelve a sí misma (fallo honesto, jamás se
        // aplica por error una optimización distinta no elegida).
        Assert.Equal(retiredId, OptimizationCatalog.CanonicalIdFor(retiredId));
    }

    // ---- BLOQUE B1: fusión gaming-power + reutilización ----

    [Fact]
    public void GamingPowerMode_RetiredAsAliasOfMaximumPowerPlan()
    {
        Assert.Contains("configure-gaming-power-mode-ac", OptimizationCatalog.LegacyIds);
        Assert.Equal("maximum-power-plan", OptimizationCatalog.CanonicalIdFor("configure-gaming-power-mode-ac"));
        Assert.DoesNotContain(OptimizationCatalog.All,
            o => o.Definition.Id.Equals("configure-gaming-power-mode-ac", StringComparison.Ordinal));
        Assert.False(CAO.Core.Optimization.OptimizationConflicts.IsPowerScheme("configure-gaming-power-mode-ac"));
    }

    [Fact]
    public async Task MaximumPowerPlan_ReusesExistingUltimate_WithoutDuplicating()
    {
        const string ultimate = MaximumPowerPlan.UltimatePerformanceGuid;
        var executor = new ScriptExecutor((key, args) => key switch
        {
            SystemCommandKey.PowerCfgSetActiveScheme when args[1].Contains("8c5e7fda", StringComparison.OrdinalIgnoreCase) =>
                Fail("High no existe"),
            SystemCommandKey.PowerCfgListSchemes =>
                Ok($"Power Scheme GUID: {ultimate} (Rendimiento máximo) *"),
            SystemCommandKey.PowerCfgSetActiveScheme => Ok(),
            _ => Fail("inesperado"),
        });
        var context = new OptimizationContext { Registry = new MemoryRegistry(), Executor = executor };

        var result = await new MaximumPowerPlan().ApplyAsync(context);

        Assert.True(result.Success);
        Assert.Contains(SystemCommandKey.PowerCfgListSchemes, executor.Calls);
        Assert.Contains(SystemCommandKey.PowerCfgSetActiveScheme, executor.Calls);
        Assert.DoesNotContain(SystemCommandKey.PowerCfgDuplicateScheme, executor.Calls);
    }

    [Fact]
    public async Task MaximumPowerPlan_DuplicatesOnlyWhenUltimateMissing()
    {
        var executor = new ScriptExecutor((key, args) => key switch
        {
            SystemCommandKey.PowerCfgSetActiveScheme when args[1].Contains("8c5e7fda", StringComparison.OrdinalIgnoreCase) =>
                Fail("High no existe"),
            SystemCommandKey.PowerCfgListSchemes =>
                Ok("Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e (Equilibrado) *"),
            SystemCommandKey.PowerCfgDuplicateScheme => Ok(),
            SystemCommandKey.PowerCfgSetActiveScheme => Ok(),
            _ => Fail("inesperado"),
        });
        var context = new OptimizationContext { Registry = new MemoryRegistry(), Executor = executor };

        var result = await new MaximumPowerPlan().ApplyAsync(context);

        Assert.True(result.Success);
        Assert.Contains(SystemCommandKey.PowerCfgDuplicateScheme, executor.Calls);
    }

    [Theory]
    [InlineData("Power Scheme GUID: e9a42b02-d5df-448d-aa00-03f14749eb61 (Ultimo) *", "e9a42b02-d5df-448d-aa00-03f14749eb61")]
    [InlineData("sin datos", null)]
    public void ParseSchemeGuids_ReadsListOutput(string output, string? expected)
    {
        var guids = MaximumPowerPlan.ParseSchemeGuids(output);
        if (expected is null)
            Assert.Empty(guids);
        else
            Assert.Contains(expected, guids);
    }

    // ---- BLOQUE B2: fusión minidump ----

    [Fact]
    public void StaleCrashDump_RetiredAsAliasOfExtended()
    {
        Assert.Contains("stale-crash-dump-cleanup", OptimizationCatalog.LegacyIds);
        Assert.Equal("cleanup-crash-dumps-extended", OptimizationCatalog.CanonicalIdFor("stale-crash-dump-cleanup"));
        Assert.DoesNotContain(OptimizationCatalog.All,
            o => o.Definition.Id.Equals("stale-crash-dump-cleanup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CrashDumpsExtended_CoversMinidump()
    {
        var preview = await new CleanupCrashDumpsExtended().PreviewAsync(new MemoryRegistry());
        Assert.Contains(preview.Lines, l => l.Target.Contains("Minidump", StringComparison.OrdinalIgnoreCase));
    }

    // ---- BLOQUE B3: Sense unificado ----

    [Theory]
    [InlineData("enable-storage-sense")]
    [InlineData("storage-sense-temp-cleanup")]
    [InlineData("storage-sense-recycle-bin-policy")]
    public void StorageSenseSingles_RetiredAsAliasOfUnified(string retiredId)
    {
        Assert.Contains(retiredId, OptimizationCatalog.LegacyIds);
        Assert.Equal("configure-storage-sense", OptimizationCatalog.CanonicalIdFor(retiredId));
        Assert.DoesNotContain(OptimizationCatalog.All,
            o => o.Definition.Id.Equals(retiredId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfigureStorageSense_WritesAllSixValuesAtOnce()
    {
        var registry = new MemoryRegistry();
        var opt = new ConfigureStorageSense();
        var result = await opt.ApplyAsync(new OptimizationContext { Registry = registry });

        Assert.True(result.Success);
        Assert.Equal(OptimizationState.AppliedByCao, opt.Detect(registry));
        foreach (var name in new[] { "01", "02", "03", "04", "05", "06" })
        {
            Assert.NotNull(registry.GetValue(RegistryHive2.CurrentUser, ConfigureStorageSense.PoliciesKey, name));
        }
        var preview = await opt.PreviewAsync(new MemoryRegistry());
        Assert.Equal(6, preview.Lines.Count);
    }

    // ---- BLOQUE C1: NIC en batería ----

    [Fact]
    public async Task NicPowerSaving_OnBattery_FailsPrecondition()
    {
        var opt = new DisableNicPowerSavingAc();
        var onBattery = await opt.CheckPreconditionsAsync(SystemContextFactory.Default() with { OnBattery = true });
        Assert.False(onBattery.Passed);
        var onAc = await opt.CheckPreconditionsAsync(SystemContextFactory.Default() with { OnBattery = false });
        Assert.True(onAc.Passed);
    }

    // ---- BLOQUE C2: GPU token-a-token ----

    [Fact]
    public void RestoreGpu_DetectIgnoresUnmanagedTokens()
    {
        var opt = new RestoreDefaultGpuPreference();
        var registry = new MemoryRegistry();
        Assert.Equal(OptimizationState.AppliedByCao, opt.Detect(registry));
        registry.SetValue(RegistryHive2.CurrentUser,
            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings",
            "GpuPreference=2;VRROptimizeEnable=1;", RegistryValueKind2.String);
        Assert.Equal(OptimizationState.NotApplied, opt.Detect(registry));
    }

    [Fact]
    public async Task RestoreGpu_ApplyStripsOnlyManagedTokens_PreservesRest()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser,
            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings",
            "GpuPreference=2;VRROptimizeEnable=1;SwapEffectUpgradeEnable=1;CustomVendor=9;", RegistryValueKind2.String);
        var opt = new RestoreDefaultGpuPreference();
        var snapshot = opt.Capture(registry);

        var applied = await opt.ApplyAsync(new OptimizationContext { Registry = registry });
        Assert.True(applied.Success);
        var after = registry.GetValue(RegistryHive2.CurrentUser,
            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings") as string;
        Assert.NotNull(after);
        Assert.DoesNotContain("GpuPreference", after, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VRROptimizeEnable", after, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SwapEffectUpgradeEnable", after, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CustomVendor=9", after, StringComparison.Ordinal);
        Assert.Equal(OptimizationState.AppliedByCao, opt.Detect(registry));

        // Revert token-a-token: vuelve cada token a su snapshot sin tocar
        // CustomVendor (aunque haya cambiado después del Apply).
        registry.SetValue(RegistryHive2.CurrentUser,
            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings",
            "CustomVendor=7;", RegistryValueKind2.String);
        var reverted = await opt.RevertAsync(new OptimizationContext { Registry = registry }, snapshot);
        Assert.True(reverted.Success);
        var restored = registry.GetValue(RegistryHive2.CurrentUser,
            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings") as string;
        Assert.NotNull(restored);
        Assert.Contains("GpuPreference=2", restored, StringComparison.Ordinal);
        Assert.Contains("VRROptimizeEnable=1", restored, StringComparison.Ordinal);
        Assert.Contains("SwapEffectUpgradeEnable=1", restored, StringComparison.Ordinal);
        Assert.Contains("CustomVendor=7", restored, StringComparison.Ordinal);
    }

    // ---- BLOQUE C3: timeouts ----

    [Fact]
    public void HeavyIds_ContainsAppCaches_NotRetiredDiskCleanup()
    {
        Assert.Contains("cleanup-app-caches", TimeoutProfile.HeavyOptimizationIds);
        Assert.DoesNotContain("disk-cleanup-system-files", TimeoutProfile.HeavyOptimizationIds);
        Assert.Contains("analyze-component-store", TimeoutProfile.HeavyOptimizationIds);
    }

    // ---- BLOQUE C4: restores fuera del lote ----

    [Theory]
    [InlineData("restore-balanced-power-dc")]
    [InlineData("restore-windows-search-default")]
    [InlineData("restore-default-gpu-preference")]
    public void SelfUndoingRestores_AreExcludedFromBatch(string id)
    {
        Assert.Contains(id, CatalogProjections.RestoreIds);
        Assert.DoesNotContain(CatalogProjections.BatchDefault,
            o => o.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    // ---- BLOQUE C6: AutoHDR defensivo ----

    [Fact]
    public void AutoHdr_RelocatedBuildThreshold_IsDocumented()
    {
        Assert.Equal(26100, EnableAutoHdr.RelocatedBuild);
        Assert.True(EnableAutoHdr.CurrentBuild() > 0);
    }

    // ---- BLOQUE D: nuevas registradas ----

    public static TheoryData<string> NewIds() => new(
    [
        "configure-storage-sense",
        "ntfs-disable-last-access",
        "disable-fast-startup",
        "cleanup-gpu-shader-cache",
        "cleanup-nvidia-downloader-cache",
        "analyze-component-store",
        "cleanup-print-spooler-jobs",
        "disable-edge-prelaunch",
        "set-boot-timeout",
    ]);

    [Theory]
    [MemberData(nameof(NewIds))]
    public async Task NewOptimizations_AreRegisteredWithPreview(string id)
    {
        var optimization = OptimizationCatalog.All.First(o => o.Definition.Id == id);
        Assert.False(string.IsNullOrWhiteSpace(optimization.Definition.NameEs));
        Assert.False(string.IsNullOrWhiteSpace(optimization.Definition.DescriptionEs));
        var preview = await optimization.PreviewAsync(new MemoryRegistry());
        Assert.Equal(id, preview.OptimizationId);
        Assert.NotEmpty(preview.Lines);
    }

    [Theory]
    [InlineData("NTFS DisableLastAccessUpdate = 1 (Disabled)", true)]
    [InlineData("NTFS DisableLastAccessUpdate = 0", false)]
    [InlineData("", false)]
    public void LastAccess_ParsesFsutilOutput(string output, bool expected)
    {
        Assert.Equal(expected, NtfsDisableLastAccess.ParseLastAccessDisabled(output));
    }

    [Fact]
    public async Task DisableFastStartup_RegistryRoundtrip()
    {
        var registry = new MemoryRegistry();
        var opt = new DisableFastStartup();
        Assert.Equal(OptimizationState.NotApplied, opt.Detect(registry));
        Assert.True((await opt.ApplyAsync(new OptimizationContext { Registry = registry })).Success);
        Assert.Equal(OptimizationState.AppliedByCao, opt.Detect(registry));
    }

    [Fact]
    public async Task DisableEdgePrelaunch_RegistryRoundtrip()
    {
        var registry = new MemoryRegistry();
        var opt = new DisableEdgePrelaunch();
        Assert.Equal(OptimizationState.NotApplied, opt.Detect(registry));
        Assert.True((await opt.ApplyAsync(new OptimizationContext { Registry = registry })).Success);
        Assert.Equal(OptimizationState.AppliedByCao, opt.Detect(registry));
    }

    [Theory]
    [InlineData("Actual Size of Component Store : 8.41 GB", "8.41 GB")]
    [InlineData("Tamaño real del almacén de componentes : 8,41 GB", "8,41 GB")]
    [InlineData("sin datos", null)]
    public void AnalyzeComponentStore_ParsesStoreSize(string output, string? expected)
    {
        Assert.Equal(expected, AnalyzeComponentStore.ParseStoreSize(output));
    }

    [Theory]
    [InlineData("Component Store Cleanup Recommended : Yes", true)]
    [InlineData("Limpieza del almacén de componentes recomendada : Sí", true)]
    [InlineData("Component Store Cleanup Recommended : No", false)]
    public void AnalyzeComponentStore_ParsesRecommendation(string output, bool expected)
    {
        Assert.Equal(expected, AnalyzeComponentStore.ParseCleanupRecommended(output));
    }

    [Theory]
    [InlineData("Windows Boot Loader\r\nWindows Boot Loader\r\ntimeout 30", 2)]
    [InlineData("Windows Boot Loader\r\ntimeout 5", 1)]
    [InlineData("Cargador de arranque de Windows\r\nCargador de arranque de Windows\r\nTiempo de espera 30", 2)]
    [InlineData("Cargador de arranque de Windows\r\nTiempo de espera 5", 1)]
    [InlineData("", 0)]
    public void BootTimeout_CountsLoaders(string output, int expected)
    {
        Assert.Equal(expected, SetBootTimeout.CountBootLoaders(output));
    }

    [Theory]
    [InlineData("timeout 30", "30")]
    [InlineData("Tiempo de espera 5", "5")]
    [InlineData("sin timeout", null)]
    public void BootTimeout_ParsesTimeout(string output, string? expected)
    {
        Assert.Equal(expected, SetBootTimeout.ParseTimeout(output));
    }

    [Fact]
    public async Task BootTimeout_SingleBoot_RefusesWithoutTouching()
    {
        var executor = new ScriptExecutor((key, _) => key switch
        {
            SystemCommandKey.BcdEditEnum => Ok("Windows Boot Loader\r\nidentifier {current}\r\ntimeout 30"),
            _ => Fail("no debe llamarse"),
        });
        var context = new OptimizationContext { Registry = new MemoryRegistry(), Executor = executor };

        var result = await new SetBootTimeout().ApplyAsync(context);

        Assert.False(result.Success);
        Assert.Contains("single-boot", result.Error ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(SystemCommandKey.BcdEditSetTimeout, executor.Calls);
    }

    [Fact]
    public async Task BootTimeout_Multiboot_SetsFive()
    {
        var executor = new ScriptExecutor((key, _) => key switch
        {
            SystemCommandKey.BcdEditEnum => Ok("Windows Boot Loader\r\nWindows Boot Loader\r\ntimeout 30"),
            SystemCommandKey.BcdEditSetTimeout => Ok(),
            _ => Fail("no debe llamarse"),
        });
        var context = new OptimizationContext { Registry = new MemoryRegistry(), Executor = executor };

        var result = await new SetBootTimeout().ApplyAsync(context);

        Assert.True(result.Success);
        Assert.Contains(SystemCommandKey.BcdEditSetTimeout, executor.Calls);
    }

    [Fact]
    public async Task PrintSpoolerJobs_EmptyQueue_NothingToDo()
    {
        // Solo el camino sin trabajos (los ficheros reales no se tocan).
        if (CleanupPrintSpoolerJobs.PendingJobs().Count != 0)
            return;
        var services = new FakeServices();
        var result = await new CleanupPrintSpoolerJobs()
            .ApplyAsync(new OptimizationContext { Registry = new MemoryRegistry(), Services = services });
        Assert.True(result.Success);
        Assert.Empty(services.Calls);
    }

    [Fact]
    public void PrintSpoolerJobs_PointsAtPrintersDir()
    {
        Assert.EndsWith(@"System32\spool\PRINTERS", CleanupPrintSpoolerJobs.PrintersDir(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GpuShaderCache_Threshold_Is50Mb()
    {
        Assert.Equal(50L * 1024 * 1024, CleanupGpuShaderCache.ThresholdBytes);
    }
}
