using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Network;
using CAO.Core.Optimizations.Power;
using CAO.Core.Optimizations.Storage;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Task 4 Plan 04: Detect reales para trim, RSS, planes de energía y wifi.
/// Registry vía MemoryRegistry (soporta GetSubKeyNames derivando subclaves
/// de valores anidados); fsutil/netsh en vivo solo no-lanzar, sin asertar
/// estado de la máquina.
/// </summary>
public sealed class RealDetectTests
{
    private const string SchemesKey = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string HighPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string CustomInactive = "12345678-1234-1234-1234-1234567890ab";
    private const string CustomActive = "abcdefab-cdef-abcd-efab-cdefabcdefab";

    private const string RssKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";

    private static MemoryRegistry SeedPowerSchemes(string active, params string[] subkeys)
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.LocalMachine, SchemesKey, "ActivePowerScheme",
            active, RegistryValueKind2.String);
        foreach (var sub in subkeys)
        {
            registry.SetValue(RegistryHive2.LocalMachine, SchemesKey + "\\" + sub,
                "FriendlyName", "Plan", RegistryValueKind2.String);
        }
        return registry;
    }

    [Fact]
    public void PowerPlansDetect_StaleCustomInactive_IsNotApplied()
    {
        var detect = new RemoveUnusedCustomPowerPlans().Detect(
            SeedPowerSchemes(Balanced, Balanced, HighPerf, CustomInactive));
        Assert.Equal(OptimizationState.NotApplied, detect);
    }

    [Fact]
    public void PowerPlansDetect_OnlyBuiltins_IsApplied()
    {
        var detect = new RemoveUnusedCustomPowerPlans().Detect(
            SeedPowerSchemes(Balanced, Balanced, HighPerf));
        Assert.Equal(OptimizationState.AppliedByCao, detect);
    }

    [Fact]
    public void PowerPlansDetect_CustomButActive_NothingStale_IsApplied()
    {
        var detect = new RemoveUnusedCustomPowerPlans().Detect(
            SeedPowerSchemes(CustomActive, Balanced, CustomActive));
        Assert.Equal(OptimizationState.AppliedByCao, detect);
    }

    [Fact]
    public void PowerPlansDetect_ThrowingRegistry_IsUnknown()
    {
        Assert.Equal(OptimizationState.Unknown,
            new RemoveUnusedCustomPowerPlans().Detect(new ThrowingRegistry()));
    }

    [Fact]
    public void RssDetect_Value1_IsApplied()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.LocalMachine, RssKey, "EnableRss", 1, RegistryValueKind2.DWord);
        Assert.Equal(OptimizationState.AppliedByCao, new EnableRss().Detect(registry));
    }

    [Fact]
    public void RssDetect_MissingOrZero_IsNotApplied()
    {
        Assert.Equal(OptimizationState.NotApplied, new EnableRss().Detect(new MemoryRegistry()));
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.LocalMachine, RssKey, "EnableRss", 0, RegistryValueKind2.DWord);
        Assert.Equal(OptimizationState.NotApplied, new EnableRss().Detect(registry));
    }

    [Fact]
    public void RssDetect_ThrowingRegistry_IsUnknown()
    {
        Assert.Equal(OptimizationState.Unknown, new EnableRss().Detect(new ThrowingRegistry()));
    }

    [Fact]
    public void ParseDeleteNotifyOff_RealFsutilOutputs()
    {
        // Salida verbatim de `fsutil behavior query DisableDeleteNotify` (EN, TRIM on).
        const string off = "NTFS DisableDeleteNotify = 0  (Allows TRIM operations to be sent to the storage device)\n"
            + "ReFS DisableDeleteNotify = 0  (Allows TRIM operations to be sent to the storage device)";
        // Misma forma con TRIM deshabilitado (= 1).
        const string on = "NTFS DisableDeleteNotify = 1  (TRIM operations are not sent to the storage device)\n"
            + "ReFS DisableDeleteNotify = 1  (TRIM operations are not sent to the storage device)";
        // Trampa del parse antiguo (Contains('0')): hay un '0' en otra línea,
        // pero DisableDeleteNotify = 1 → debe ser false.
        const string decoy = "Alguna cabecera con numero 0\nNTFS DisableDeleteNotify = 1\nReFS DisableDeleteNotify = 1";

        Assert.True(EnsureTrimEnabled.ParseDeleteNotifyOff(off));
        Assert.False(EnsureTrimEnabled.ParseDeleteNotifyOff(on));
        Assert.False(EnsureTrimEnabled.ParseDeleteNotifyOff(decoy));
        Assert.False(EnsureTrimEnabled.ParseDeleteNotifyOff(string.Empty));
    }

    [Fact]
    public void ParseAutoconfigState_KnownShapes()
    {
        const string disabled = "Wireless LAN settings\n---------------------\n"
            + "    Auto configuration logic is disabled on interface \"Wi-Fi\"";
        const string enabled = "Wireless LAN settings\n---------------------\n"
            + "    Auto configuration logic is enabled on interface \"Wi-Fi\"";

        Assert.Equal(false, DisableWifiBackgroundScan.ParseAutoconfigState(disabled, "Wi-Fi"));
        Assert.Equal(true, DisableWifiBackgroundScan.ParseAutoconfigState(enabled, "Wi-Fi"));
        Assert.Null(DisableWifiBackgroundScan.ParseAutoconfigState(enabled, "Otra"));
        Assert.Null(DisableWifiBackgroundScan.ParseAutoconfigState("sin datos", "Wi-Fi"));
    }

    [Fact]
    public void LiveDetect_NeverThrows()
    {
        var registry = new MemoryRegistry();
        Assert.True(Enum.IsDefined(new EnsureTrimEnabled().Detect(registry)));
        Assert.True(Enum.IsDefined(new RemoveUnusedCustomPowerPlans().Detect(registry)));
        Assert.True(Enum.IsDefined(new DisableWifiBackgroundScan().Detect(registry)));
        Assert.True(Enum.IsDefined(new EnableRss().Detect(registry)));
    }

    [Fact]
    public void WifiDetect_WithoutWifi_IsUnknown()
    {
        // Esta máquina/CI no tiene adaptador Wi-Fi: sin interfaz no hay
        // estado que leer; Unknown (nunca NotApplied ciego: RULING Plan 04).
        // Con Wi-Fi el Detect real observa netsh: cualquier valor definido vale.
        var state = new DisableWifiBackgroundScan().Detect(new MemoryRegistry());
        if (!HasWifi())
        {
            Assert.Equal(OptimizationState.Unknown, state);
        }
        else
        {
            Assert.True(Enum.IsDefined(state));
        }
    }

    private static bool HasWifi()
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Any(n => n.NetworkInterfaceType
                    == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Registry que lanza en todo: Detect debe degradar a Unknown.</summary>
    private sealed class ThrowingRegistry : IRegistryAccessor
    {
        public RegistryValueKind2 GetKind(RegistryHive2 hive, string keyPath, string valueName) =>
            throw new InvalidOperationException("sin registro");
        public object? GetValue(RegistryHive2 hive, string keyPath, string valueName) =>
            throw new InvalidOperationException("sin registro");
        public object? GetValueRaw(RegistryHive2 hive, string keyPath, string valueName, out RegistryValueKind2 kind) =>
            throw new InvalidOperationException("sin registro");
        public void SetValue(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
            throw new InvalidOperationException("sin registro");
        public void SetValueRaw(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
            throw new InvalidOperationException("sin registro");
        public bool DeleteValue(RegistryHive2 hive, string keyPath, string valueName) =>
            throw new InvalidOperationException("sin registro");
        public IReadOnlyList<string> GetValueNames(RegistryHive2 hive, string keyPath) =>
            throw new InvalidOperationException("sin registro");
        public IReadOnlyList<string> GetSubKeyNames(RegistryHive2 hive, string keyPath) =>
            throw new InvalidOperationException("sin registro");
    }
}
