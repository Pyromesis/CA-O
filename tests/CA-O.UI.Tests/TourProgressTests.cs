using System;
using System.IO;
using System.Text.Json;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.UI.Tutorial;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>A store that fails every operation, to prove TourProgress never lets an exception escape.</summary>
sealed class ThrowingSettingsStore : ISettingsStore
{
    public AppSettings Load() => throw new IOException("settings.json is locked");

    public void Save(AppSettings settings) => throw new IOException("settings.json is locked");
}

/// <summary>
/// TourProgress against the shared <see cref="FakeSettingsStore"/> (declared in
/// TrayPreferencesTests.cs). Every test injects a store through
/// <see cref="TourProgress.StoreOverride"/>, so none of them can reach
/// AppHost.Resolve -> JsonSettingsStore -> %AppData%\CA-O\settings.json.
/// </summary>
public sealed class TourProgressTests : IDisposable
{
    public TourProgressTests() => TourProgress.StoreOverride = null;

    public void Dispose() => TourProgress.StoreOverride = null;

    private static FakeSettingsStore CreateStore()
    {
        // Seed with values that are NOT the factory defaults, so a writer that
        // reconstructs the model instead of mutating the loaded one is caught.
        var seed = new AppSettings
        {
            Version = "2.2.3",
            Ui = new UiSettings
            {
                Theme = "dark",
                Language = "es-ES",
                ExpertMode = true,
                ReadOnlyMode = true,
                MinimizeToTray = true,
                CloseToTray = true,
            },
            SystemInfo = new SystemInfoCache { RamGb = 64, DiskType = "NVMe" },
        };
        seed.Entry("svc").Enabled = true;
        return new FakeSettingsStore { Current = seed };
    }

    /// <summary>
    /// Serialise + deserialize, so "a fresh read" cannot be satisfied by the very same
    /// in-memory object the writer mutated.
    /// </summary>
    private static AppSettings FreshRead(FakeSettingsStore store) =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(store.Current))!;

    [Fact]
    public void MarkCompleted_ThenFreshRead_ReportsCompleted()
    {
        var store = CreateStore();
        TourProgress.StoreOverride = store;

        TourProgress.MarkCompleted();

        Assert.Equal(1, store.Saves);
        Assert.True(FreshRead(store).Ui.TourCompleted);
        Assert.True(TourProgress.IsCompleted());
        Assert.False(TourProgress.IsDismissed());
    }

    [Fact]
    public void MarkDismissed_ThenFreshRead_ReportsDismissed()
    {
        var store = CreateStore();
        TourProgress.StoreOverride = store;

        TourProgress.MarkDismissed();

        Assert.Equal(1, store.Saves);
        Assert.True(FreshRead(store).Ui.TourDismissed);
        Assert.True(TourProgress.IsDismissed());
        Assert.False(TourProgress.IsCompleted());
    }

    /// <summary>
    /// Regression guard for the load-mutate-save invariant: a writer that reconstructs
    /// the settings model (store.Save(new AppSettings())) wipes every pre-existing
    /// setting. This test must fail if TourProgress.Write ever stops mutating the
    /// object returned by Load().
    /// </summary>
    [Fact]
    public void Write_PreservesEveryUnrelatedPreExistingSetting()
    {
        var store = CreateStore();
        TourProgress.StoreOverride = store;

        TourProgress.MarkCompleted();
        TourProgress.MarkDismissed();

        var after = store.Current;

        // Asserted first: a writer that clobbers fails here, on lost user data,
        // not merely on a missing tour flag.
        Assert.Equal("2.2.3", after.Version);
        Assert.Equal("dark", after.Ui.Theme);
        Assert.Equal("es-ES", after.Ui.Language);
        Assert.True(after.Ui.ExpertMode);
        Assert.True(after.Ui.ReadOnlyMode);
        Assert.True(after.Ui.MinimizeToTray);
        Assert.True(after.Ui.CloseToTray);
        Assert.Equal(64, after.SystemInfo.RamGb);
        Assert.Equal("NVMe", after.SystemInfo.DiskType);
        Assert.True(after.Entry("svc").Enabled);

        Assert.True(after.Ui.TourCompleted);
        Assert.True(after.Ui.TourDismissed);
    }

    /// <summary>
    /// Proof that the real settings file is never reached. If Store() fell through to
    /// AppHost.Resolve here, the write would silently no-op (the container is not
    /// initialised in the test host and the throw is swallowed), leaving Saves at 0.
    /// </summary>
    [Fact]
    public void Public_api_writes_through_the_injected_store_only()
    {
        var store = CreateStore();
        TourProgress.StoreOverride = store;

        TourProgress.MarkCompleted();
        TourProgress.MarkDismissed();

        Assert.Equal(2, store.Saves);
        Assert.True(FreshRead(store).Ui.TourCompleted);
        Assert.True(FreshRead(store).Ui.TourDismissed);
    }

    [Fact]
    public void Neither_getter_nor_writer_throws_when_the_store_fails()
    {
        TourProgress.StoreOverride = new ThrowingSettingsStore();

        Assert.False(TourProgress.IsCompleted());
        Assert.False(TourProgress.IsDismissed());
        TourProgress.MarkCompleted();
        TourProgress.MarkDismissed();
    }
}