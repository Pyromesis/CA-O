using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CAO.UI.Controls;

/// <summary>Reusable diagnostic section container (Analyze + Diagnostics share same component, no duplicated logic).</summary>
public sealed class DiagnosticSection : UserControl
{
    private readonly TextBlock _header;
    private readonly StackPanel _contentPanel;

    public DiagnosticSection()
    {
        var borderStyle = (Style)Application.Current.Resources["CaoCardStyle"];
        var border = new Border { Style = borderStyle };
        var root = new StackPanel { Spacing = 8 };
        _header = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 };
        _contentPanel = new StackPanel { Spacing = 6 };
        root.Children.Add(_header);
        root.Children.Add(_contentPanel);
        border.Child = root;
        Content = border;
    }

    public string Header { get => _header.Text; set => _header.Text = value; }
    public StackPanel ContentPanel => _contentPanel;
}
