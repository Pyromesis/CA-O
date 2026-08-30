using CAO.UI.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CAO.Shared;

namespace CAO.UI;

/// <summary>WinUI 3 shell (spec 76-78): NavigationView, Mica, page routing, TopBar + sidebar context.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Lets any page re-theme every window root.</summary>
    public static Action<string>? ApplyThemeGlobally { get; private set; }

    public static new MainWindow? Current { get; private set; }
    public void SelectRoute(string tag)
    {
        foreach (var item in EnumerateItems())
        {
            if (item is NavigationViewItem nvi && nvi.Tag is string t && t == tag)
            {
                Nav.SelectedItem = nvi;
                var pageType = Navigation.RouteTable.Resolve(tag);
                if (pageType != null) ContentFrame.Navigate(pageType);
                return;
            }
        }
    }

    public MainWindow()
    {
        Current = this;
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.LanguageChanged += (_, language) => ApplyLocalization();
        uiState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.UiState.ServiceStatus) or nameof(ViewModels.UiState.Context) or nameof(ViewModels.UiState.LastAnalysisUtc) or nameof(ViewModels.UiState.Recommendations))
                DispatcherQueue.TryEnqueue(() => RefreshChrome());
        };
        ApplyLocalization();
        RefreshChrome();

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
        // Kick off background service probe without blocking first frame (Fase 25).
        _ = ProbeServiceAsync();
    }

    private void ApplyLocalization()
    {
        // Ensure dictionary matches selected language before any Get()
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        Localizer.SetLanguage(uiState.Language);
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

        TopBarTitle.Text = Localizer.Get("app.title");
        TopBarSubtitle.Text = Localizer.Get("app.subtitle");
        Title = $"{Localizer.Get("app.title")} — {Localizer.Get("app.subtitle")}";
        RefreshChrome();
    }

    private void RefreshChrome()
    {
        var state = AppHost.Resolve<ViewModels.UiState>();
        var ctx = state.Context;

        // Sidebar bottom context (Fase 3 spec)
        SidebarBuildText.Text = ctx is null ? "Windows: —" : $"Windows build {ctx.WindowsBuild} · {ctx.WindowsEdition}";
        SidebarLastScan.Text = state.LastAnalysisUtc is null ? $"{Localizer.Get("common.lastScan")}: {Localizer.Get("dashboard.never")}" : $"{Localizer.Get("common.lastScan")}: {state.LastAnalysisUtc.Value.ToLocalTime():g}";
        LastScanTopText.Text = SidebarLastScan.Text;

        // Service status top + sidebar
        var svc = state.ServiceStatus ?? "unknown";
        var svcLabel = svc switch
        {
            "connected" or "conectado" => Localizer.Get("common.connected"),
            "unavailable" or "no disponible" or "unknown" => Localizer.Get("common.disconnected"),
            _ => svc
        };
        ServiceTopText.Text = $"{Localizer.Get("common.serviceStatus")}: {(svcLabel == "unknown" ? Localizer.Get("common.disconnected") : svcLabel)}";
        SidebarServiceStatus.Text = ServiceTopText.Text;
        ServiceDot.Fill = (svc is "connected" or "conectado")
            ? (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
            : (svc is "rejected" ? (Brush)Application.Current.Resources["SystemFillColorCautionBrush"] : (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]);

        // System health summary
        if (ctx is null)
        {
            SystemTopText.Text = $"{Localizer.Get("common.systemStatus")}: —";
            SidebarSystemStatus.Text = SystemTopText.Text;
        }
        else
        {
            var thermal = ctx.ThermalState == ThermalState.Throttling ? Localizer.Get("common.warning") : Localizer.Get("common.healthy");
            var reboot = ctx.PendingReboot ? $" · {Localizer.Get("common.warning")}: reinicio pendiente" : "";
            SystemTopText.Text = $"{Localizer.Get("common.systemStatus")}: {thermal}{reboot}";
            SidebarSystemStatus.Text = SystemTopText.Text;
        }

        // Operation indicator (Phase 13) — driven by OptimizePage via AppServices.State if needed.
        if (state.Recommendations.Count > 0 && state.LastAnalysisUtc is not null)
        {
            var rec = state.Recommendations.Count(r => r.Bucket == RecommendationBucket.Recommended);
            OperationTopText.Text = rec > 0 ? $"{rec} recomendadas" : "";
        }
    }

    private async Task ProbeServiceAsync()
    {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var pipe = AppHost.Resolve<PrivilegedPipeClient>();
        try
        {
            var resp = await pipe.DetectAsync("disable-transparency");
            uiState.ServiceStatus = resp is { Accepted: true } ? "connected" : "rejected";
        }
        catch
        {
            uiState.ServiceStatus = "unavailable";
        }
        DispatcherQueue.TryEnqueue(RefreshChrome);
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
