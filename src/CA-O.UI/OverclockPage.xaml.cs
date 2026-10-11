using CAO.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CAO.UI.Pages;

public sealed partial class OverclockPage : Page
{
    public OverclockPage()
    {
        ViewModel = new OverclockViewModel();
        InitializeComponent();
    }

    public OverclockViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
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