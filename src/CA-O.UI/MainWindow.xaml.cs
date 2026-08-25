using CAO.UI.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CAO.UI;

/// <summary>WinUI 3 shell (spec 76-78): NavigationView, Mica, page routing.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Lets any page re-theme every window root.</summary>
    public static Action<string>? ApplyThemeGlobally { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        AppServices.State.LanguageChanged += (_, language) => ApplyLocalization();
        ApplyLocalization();

        ApplyThemeGlobally = theme =>
        {
            if (Content is FrameworkElement root)
            {
                root.RequestedTheme = theme switch
                {
                    "light" => ElementTheme.Light,
                    "dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default,
                };
            }
        };

        Nav.SelectedItem = Nav.MenuItems[0];
    }

    private void ApplyLocalization()
    {
        var navItems = new (string Tag, string Key)[]
        {
            ("dashboard", "nav.dashboard"),
            ("analyze", "nav.analyze"),
            ("optimize", "nav.optimize"),
            ("gaming", "nav.gaming"),
            ("diagnostics", "nav.diagnostics"),
            ("benchmark", "nav.benchmark"),
            ("restore", "nav.restore"),
            ("history", "nav.history"),
            ("settings", "nav.settings"),
        };

        foreach (var item in EnumerateItems())
        {
            if (item.Tag is string tag)
            {
                var match = Array.Find(navItems, entry => entry.Tag == tag);
                if (match.Tag is not null)
                {
                    item.Content = Localizer.Get(match.Key);
                }
            }
        }

        Title = $"{Localizer.Get("app.title")} — {Localizer.Get("app.subtitle")}";
    }

    private System.Collections.Generic.IEnumerable<NavigationViewItemBase> EnumerateItems()
    {
        foreach (var item in Nav.MenuItems.OfType<NavigationViewItemBase>())
        {
            yield return item;
        }
        foreach (var item in Nav.FooterMenuItems.OfType<NavigationViewItemBase>())
        {
            yield return item;
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        var pageType = Navigation.RouteTable.Resolve(tag);
        if (pageType is not null)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
