using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CAO.UI.Controls;

/// <summary>Metric card specialized for diagnostics (value + status badge).</summary>
public sealed class DiagnosticMetricCard : UserControl
{
    private readonly TextBlock _label;
    private readonly TextBlock _value;
    private readonly Border _badge;
    private readonly TextBlock _badgeText;

    public DiagnosticMetricCard()
    {
        var style = (Style)Application.Current.Resources["CaoMetricCardStyle"];
        var border = new Border { Style = style };
        var panel = new StackPanel { Spacing = 6 };
        _label = new TextBlock { FontSize = 11, Opacity = 0.7, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _value = new TextBlock { FontSize = 13, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        _badgeText = new TextBlock { FontSize = 11 };
        _badge = new Border { Style = (Style)Application.Current.Resources["CaoBadgeStyle"], Visibility = Visibility.Collapsed, Child = _badgeText };
        panel.Children.Add(_label);
        panel.Children.Add(_value);
        panel.Children.Add(_badge);
        border.Child = panel;
        Content = border;
    }

    public string Label { get => _label.Text; set => _label.Text = value; }
    public string Value { get => _value.Text; set => _value.Text = value; }
    public void SetStatus(string text, Brush brush) { _badgeText.Text = text; _badge.Background = brush; _badge.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible; }
}
