using System.Text.Json;
using CAO.Shared;
using Xunit;

namespace CAO.UI.Tests;

public sealed class TourSettingsTests
{
    [Fact]
    public void Tour_flags_default_to_false_and_round_trip_through_json()
    {
        var settings = new AppSettings();
        Assert.False(settings.Ui.TourCompleted);
        Assert.False(settings.Ui.TourDismissed);

        settings.Ui.TourCompleted = true;
        settings.Ui.TourDismissed = true;

        var json = JsonSerializer.Serialize(settings);
        Assert.Contains("\"tour_completed\":true", json);
        Assert.Contains("\"tour_dismissed\":true", json);

        var restored = JsonSerializer.Deserialize<AppSettings>(json);
        Assert.NotNull(restored);
        Assert.True(restored.Ui.TourCompleted);
        Assert.True(restored.Ui.TourDismissed);
    }

    [Fact]
    public void Existing_settings_json_without_tour_flags_still_loads()
    {
        const string legacy = "{\"version\":\"2.2.3\",\"ui_settings\":{\"theme\":\"system\",\"language\":\"es-ES\",\"expert_mode\":false,\"read_only_mode\":false,\"minimize_to_tray\":true,\"close_to_tray\":true}}";
        var restored = JsonSerializer.Deserialize<AppSettings>(legacy);
        Assert.NotNull(restored);
        Assert.False(restored.Ui.TourCompleted);
        Assert.False(restored.Ui.TourDismissed);
        Assert.True(restored.Ui.MinimizeToTray);
    }
}