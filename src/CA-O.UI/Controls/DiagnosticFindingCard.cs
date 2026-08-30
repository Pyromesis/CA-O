using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CAO.UI.Controls;

public sealed class DiagnosticFindingCard : UserControl
{
    private readonly TextBlock _title;
    private readonly TextBlock _desc;
    private readonly Border _severity;
    public DiagnosticFindingCard()
    {
        var style = (Style)Application.Current.Resources["CaoCardStyle"];
        var border = new Border { Style = style };
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _severity = new Border { Width = 4, CornerRadius = new CornerRadius(2) };
        Grid.SetColumn(_severity, 0);
        var panel = new StackPanel { Spacing = 2 };
        _title = new TextBlock { FontSize = 11, Opacity = 0.65, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _desc = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(_title);
        panel.Children.Add(_desc);
        Grid.SetColumn(panel, 1);
        grid.Children.Add(_severity);
        grid.Children.Add(panel);
        border.Child = grid;
        Content = border;
    }
    public string Title { get => _title.Text; set => _title.Text = value; }
    public string Description { get => _desc.Text; set => _desc.Text = value; }
    public void SetSeverity(Brush brush) => _severity.Background = brush;
}
