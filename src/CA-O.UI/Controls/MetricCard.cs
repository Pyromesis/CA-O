using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CAO.UI.Controls;

/// <summary>
/// Componente reutilizable MetricCard (§55, §162) — Border con jerarquía consistente, evita duplicación XAML.
/// Usa tokens CaoCardStyle / CaoMetricCardStyle y soporta virtualization en listas grandes (§160).
/// </summary>
public sealed class MetricCard : UserControl
{
    private readonly TextBlock _title;
    private readonly TextBlock _value;
    private readonly TextBlock _detail;
    private readonly Border _badge;
    private readonly TextBlock _badgeText;

    public MetricCard()
    {
        var borderStyle = (Style)Application.Current.Resources["CaoMetricCardStyle"];
        var card = new Border { Style = borderStyle };
        var panel = new StackPanel { Spacing = 6 };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _title = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 };
        _badgeText = new TextBlock { FontSize = 11 };
        _badge = new Border
        {
            Style = (Style)Application.Current.Resources["CaoBadgeStyle"],
            Visibility = Visibility.Collapsed,
            Child = _badgeText
        };
        header.Children.Add(_title);
        header.Children.Add(_badge);
        _value = new TextBlock { FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _detail = new TextBlock { FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(header);
        panel.Children.Add(_value);
        panel.Children.Add(_detail);
        card.Child = panel;
        Content = card;
    }

    public string Title { get => _title.Text; set => _title.Text = value; }
    public string Value { get => _value.Text; set => _value.Text = value; }
    public string Detail { get => _detail.Text; set => _detail.Text = value; }

    public void SetBadge(string text, Brush brush)
    {
        _badgeText.Text = text;
        _badge.Background = brush;
        _badge.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
