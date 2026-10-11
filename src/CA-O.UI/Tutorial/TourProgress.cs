using System;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.UI.Tutorial;

/// <summary>
/// Persistent tour progress. Reads and writes the two flags on UiSettings using the
/// codebase idiom: load the model, mutate one flag, save it back.
/// </summary>
public static class TourProgress
{
    /// <summary>
    /// Test seam. When set, the four public methods read and write through this store
    /// instead of <see cref="AppHost"/>. Lets tests exercise the real code path without
    /// ever reaching the real %AppData%\CA-O\settings.json. Never set in production.
    /// </summary>
    internal static ISettingsStore? StoreOverride { get; set; }

    private static ISettingsStore? Store()
    {
        var injected = StoreOverride;
        if (injected is not null) return injected;

        try
        {
            return AppHost.Resolve<ISettingsStore>();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            return null;
        }
    }

    private static bool Read(Func<UiSettings, bool> pick)
    {
        var store = Store();
        if (store is null) return false;
        try
        {
            var settings = store.Load();
            return settings is not null && pick(settings.Ui);
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            return false;
        }
    }

    private static void Write(Action<UiSettings> mutate)
    {
        var store = Store();
        if (store is null) return;
        try
        {
            var settings = store.Load();
            if (settings is null) return;
            mutate(settings.Ui);
            store.Save(settings);
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
        }
    }

    public static bool IsCompleted() => Read(u => u.TourCompleted);

    public static bool IsDismissed() => Read(u => u.TourDismissed);

    public static void MarkCompleted() => Write(u => u.TourCompleted = true);

    public static void MarkDismissed() => Write(u => u.TourDismissed = true);
}
