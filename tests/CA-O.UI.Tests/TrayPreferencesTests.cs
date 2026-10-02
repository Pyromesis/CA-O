using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

sealed class FakeSettingsStore : ISettingsStore
{
    public AppSettings Current = new();
    public int Saves;

    public AppSettings Load() => Current;

    public void Save(AppSettings settings)
    {
        Saves++;
        Current = settings;
    }
}

/// <summary>Tray preferences (§tray): defaults, VM→state propagation, persistence, hydration.</summary>
public sealed class TrayPreferencesTests
{
    private static (SettingsViewModel vm, UiState state, FakeSettingsStore store) CreateVms(AppSettings? seed = null)
    {
        var store = new FakeSettingsStore();
        if (seed != null) store.Current = seed;
        var state = new UiState();
        var vm = new SettingsViewModel(state, new CAO.UI.PrivilegedPipeClient(), store);
        return (vm, state, store);
    }

    [Fact]
    public void TrayPrefs_DefaultToTrue()
    {
        var (vm, state, _) = CreateVms();
        Assert.True(vm.MinimizeToTray);
        Assert.True(vm.CloseToTray);
        Assert.True(state.MinimizeToTray);
        Assert.True(state.CloseToTray);
    }

    [Fact]
    public void MinimizeToTray_Toggle_PropagatesToStateAndPersists()
    {
        var (vm, state, store) = CreateVms();
        vm.MinimizeToTray = false;
        Assert.False(state.MinimizeToTray);
        Assert.True(store.Saves > 0);
        Assert.False(store.Current.Ui.MinimizeToTray);
    }

    [Fact]
    public void CloseToTray_Toggle_PropagatesToStateAndPersists()
    {
        var (vm, state, store) = CreateVms();
        vm.CloseToTray = false;
        Assert.False(state.CloseToTray);
        Assert.True(store.Saves > 0);
        Assert.False(store.Current.Ui.CloseToTray);
    }

    [Fact]
    public void TrayPrefs_HydrateFromStore()
    {
        var seed = new AppSettings();
        seed.Ui.MinimizeToTray = false;
        seed.Ui.CloseToTray = false;
        var (vm, state, _) = CreateVms(seed);
        Assert.False(vm.MinimizeToTray);
        Assert.False(vm.CloseToTray);
        Assert.False(state.MinimizeToTray);
        Assert.False(state.CloseToTray);
    }
}
