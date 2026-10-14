using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CAO.UI.ViewModels;

/// <summary>
/// x:Bind functions for the Overclock page templates. Static and total: a
/// missing resource or an empty string degrades to a neutral look, never an
/// exception inside a DataTemplate.
/// </summary>
public static class OverclockXaml
{
    /// <summary>Hides a detail line that has nothing to say, so rows stop showing empty gaps.</summary>
    public static Visibility VisibleWhen(string? text) =>
        string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Badge tint per control state: green writable, grey read-only, amber blocked.</summary>
    public static Brush? StateBrush(OverclockControlState state) => Resource(state switch
    {
        OverclockControlState.Available => "CaoGreenSoftBrush",
        OverclockControlState.Unsupported => "CaoSlateSoftBrush",
        OverclockControlState.Applying => "CaoBlueSoftBrush",
        _ => "CaoAmberSoftBrush",
    });

    private static Brush? Resource(string key)
    {
        try
        {
            return Application.Current?.Resources.TryGetValue(key, out var value) == true ? value as Brush : null;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }
}
