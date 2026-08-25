using CAO.Core.Abstractions;
using CAO.Core.Catalog;
using CAO.Core.Gaming;
using CAO.Shared;
using Xunit;

namespace CAO.Integration.Tests;

/// <summary>In-memory registry (local copy for preview dry-runs).</summary>
public sealed class PreviewRegistry : IRegistryAccessor
{
    public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static string K(RegistryHive2 h, string p, string n) => $"{h}:{p}\\{n}";

    public RegistryValueKind2 GetKind(RegistryHive2 hive, string keyPath, string valueName) =>
        GetValue(hive, keyPath, valueName) is string ? RegistryValueKind2.String : RegistryValueKind2.DWord;

    public object? GetValue(RegistryHive2 hive, string keyPath, string valueName) =>
        Values.TryGetValue(K(hive, keyPath, valueName), out var v) ? v : null;

    public object? GetValueRaw(RegistryHive2 hive, string keyPath, string valueName, out RegistryValueKind2 kind)
    {
        kind = GetKind(hive, keyPath, valueName);
        return GetValue(hive, keyPath, valueName);
    }

    public void SetValue(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
        Values[K(hive, keyPath, valueName)] = value;

    public void SetValueRaw(RegistryHive2 hive, string keyPath, string valueName, object value, RegistryValueKind2 kind) =>
        SetValue(hive, keyPath, valueName, value, kind);

    public bool DeleteValue(RegistryHive2 hive, string keyPath, string valueName) =>
        Values.Remove(K(hive, keyPath, valueName));

    public IReadOnlyList<string> GetValueNames(RegistryHive2 hive, string keyPath) => [];
}

/// <summary>
/// Dry-run previews (FASE 40/41) and the game-profile catalog contract
/// (FASE 22): previews must produce per-value diffs without mutating; the
/// game catalog must stay conservative and evidence-tagged.
/// </summary>
public sealed class PreviewAndGameProfileTests
{
    private readonly PreviewRegistry _registry = new();

    [Fact]
    public async Task RegistryOptimizationPreviewShowsBeforeAndAfter()
    {
        var optimization = OptimizationCatalog.All.First(o => o.Definition.Id == "disable-transparency");

        // Current value deliberately different from applied value.
        _registry.SetValue(RegistryHive2.CurrentUser,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "EnableTransparency", 1, RegistryValueKind2.DWord);

        var preview = await optimization.PreviewAsync(_registry);

        Assert.Equal("disable-transparency", preview.OptimizationId);
        Assert.NotEmpty(preview.Lines);
        Assert.All(preview.Lines, line =>
        {
            Assert.Equal("Registry", line.Kind);
            Assert.Contains("(", line.Target); // kind name present
        });
    }

    [Fact]
    public void GameCatalogIsConservative()
    {
        Assert.NotEmpty(GameProfileCatalog.All);

        foreach (var profile in GameProfileCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(profile.DisplayName));
            Assert.NotEmpty(profile.Executables);
            Assert.NotEmpty(profile.GuidanceEs);

            if (profile.AntiCheatPolicy == GameAntiCheatPolicy.KernelLevel)
            {
                Assert.True(profile.IsKernelProtected());
            }
        }
    }

    [Fact]
    public void FindByExecutableMatchesShippingNames()
    {
        Assert.NotNull(GameProfileCatalog.FindByExecutable("cs2.exe"));
        Assert.NotNull(GameProfileCatalog.FindByExecutable("VALORANT-Win64-Shipping.exe"));
        Assert.Null(GameProfileCatalog.FindByExecutable("notepad.exe"));
    }
}
