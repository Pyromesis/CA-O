using CAO.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CAO.UI.Pages;

public sealed partial class OverclockPage : Page
{
    /// <summary>Boton del tour de esta pagina: texto localizado y lanzamiento con guarda. Nunca lanza.</summary>
    private void ApplyPageTourText()
    {
        try
        {
            PageTourButton.Content = Localizer.Get("tour.page.button");
            ToolTipService.SetToolTip(PageTourButton, Localizer.Get("tour.page.tip"));
        }
        catch { }
    }

    private void OnPageTourClick(object sender, RoutedEventArgs e)
    {
        if (Tutorial.TourCoordinator.IsActive) return;
        MainWindow.Current?.StartPageTour("overclock");
    }

    public OverclockPage()
    {
        ViewModel = new OverclockViewModel();
        InitializeComponent();
        ApplyPageTourText();
    }

    public OverclockViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyPageTourText();
        try
        {
            await ViewModel.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            // The awaited body already guards itself; this is the last net so a
            // navigation-time fault never escapes into the shell.
            App.WriteCrashLog(ex);
        }
    }
}