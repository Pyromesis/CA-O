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

    /// <summary>Abre la sección y lanza su tour cuando la página ya está en pantalla.</summary>
    private void OnChapterTourClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string route } || TourCoordinator.IsActive) return;
        var window = MainWindow.Current;
        if (window is null) return;
        window.SelectRoute(route);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => window.StartPageTour(route));
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ChaptersList.ItemsSource = TourCatalog.Chapters;
        Helpers.UiAnimations.PlayEntrance(PageContent);
    }
}
