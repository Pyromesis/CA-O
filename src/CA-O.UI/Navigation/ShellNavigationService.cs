using Microsoft.UI.Xaml.Controls;

namespace CAO.UI.Navigation;

public sealed class ShellNavigationService : INavigationService
{
    private readonly NavigationView _nav;
    private readonly Frame _frame;

    public ShellNavigationService(NavigationView nav, Frame frame)
    {
        _nav = nav;
        _frame = frame;
    }

    public string? CurrentRoute { get; private set; }

    public bool CanNavigate(string route) => RouteTable.Resolve(route) != null;

    public void Navigate(string route)
    {
        var page = RouteTable.Resolve(route);
        if (page != null)
        {
            CurrentRoute = route;
            _frame.Navigate(page);
        }
    }

    public void Select(string route)
    {
        // Find NavigationViewItem with Tag == route and select it; OnNavigationSelectionChanged will handle Navigate
        foreach (var item in Enumerate())
        {
            if (item is NavigationViewItem nvi && nvi.Tag is string tag && tag == route)
            {
                _nav.SelectedItem = nvi;
                return;
            }
        }
        // fallback direct
        Navigate(route);
    }

    private IEnumerable<NavigationViewItemBase> Enumerate()
    {
        foreach (var i in _nav.MenuItems.OfType<NavigationViewItemBase>()) yield return i;
        foreach (var i in _nav.FooterMenuItems.OfType<NavigationViewItemBase>()) yield return i;
    }
}
