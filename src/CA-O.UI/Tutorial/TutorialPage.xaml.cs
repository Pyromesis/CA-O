using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CAO.UI.Tutorial;

/// <summary>
/// Always-available tutorial section. The chapter list is generated at runtime from
/// TourCatalog.Chapters so no chapter text is duplicated in XAML.
/// </summary>
public sealed partial class TutorialPage : Page
{
    public TutorialPage()
    {
        InitializeComponent();
        ChaptersList.ItemsSource = TourCatalog.Chapters;
    }

    private void OnStartTourClick(object sender, RoutedEventArgs e)
    {
        MainWindow.Current?.StartTour();
    }

    private void OnChapterClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string route })
        {
            MainWindow.Current?.SelectRoute(route);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ChaptersList.ItemsSource = TourCatalog.Chapters;
    }
}
