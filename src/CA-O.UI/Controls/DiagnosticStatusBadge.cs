using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CAO.UI.Controls;

public sealed class DiagnosticStatusBadge : UserControl
{
    private readonly Border _badge;
    private readonly TextBlock _text;
    public DiagnosticStatusBadge()
    {
        _text = new TextBlock { FontSize = 11 };
        _badge = new Border { Style = (Style)Application.Current.Resources["CaoBadgeStyle"], Child = _text, Visibility = Visibility.Collapsed };
        Content = _badge;
    }
    public void Set(string label, Brush background) { _text.Text = label; _badge.Background = background; _badge.Visibility = string.IsNullOrWhiteSpace(label) ? Visibility.Collapsed : Visibility.Visible; }
}
