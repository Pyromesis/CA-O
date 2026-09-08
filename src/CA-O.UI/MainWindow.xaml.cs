using CAO.UI.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using CAO.Shared;
using System.IO;

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
        TrySetWindowIcon();
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
        // Chequeo de actualización en segundo plano: solo avisa, nunca descarga solo.
        _ = CheckForUpdatesAsync();
        // Usuario nuevo: análisis inicial obligatorio antes de usar la app.
        _ = EnsureInitialAnalysisAsync();
    }

    private static bool NeedsInitialAnalysis() =>
        AppHost.Resolve<ViewModels.UiState>().LastAnalysisUtc is null;

    private async Task EnsureInitialAnalysisAsync()
    {
        try
        {
            await Task.Delay(600); // dejar pintar el primer frame
            if (!NeedsInitialAnalysis()) return;
            SelectRoute("analyze");
            await PromptInitialAnalysisAsync();
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
    }

    /// <summary>
    /// Diálogo no descartable: la única salida es ejecutar el análisis.
    /// Cualquier ejecución completa (aunque sea con advertencias) desbloquea.
    /// </summary>
    private async Task PromptInitialAnalysisAsync()
    {
        while (NeedsInitialAnalysis())
        {
            var dialog = new ContentDialog
            {
                Title = "Análisis inicial obligatorio",
                Content = "Antes de usar CA-O necesitas un análisis completo: mide tu CPU, GPU, red y seguridad para generar recomendaciones con evidencia. No modifica nada del sistema.",
                PrimaryButtonText = "Ejecutar análisis ahora",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            await dialog.ShowAsync();
            try
            {
                if (ContentFrame.Content is Pages.AnalyzePage page)
                    await page.RunFullAnalysisAsync();
            }
            catch (Exception ex) { App.WriteCrashLog(ex); }
        }
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
            ("cleanup", "nav.cleanup"),
            ("solucionar", "nav.solucionar"),
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
            "rejected" or "rechazado" => Localizer.Get("common.rejected"),
            "unavailable" or "no disponible" or "unknown" => Localizer.Get("common.disconnected"),
            _ => svc
        };
        ServiceTopText.Text = $"{Localizer.Get("common.serviceStatus")}: {(svcLabel == "unknown" ? Localizer.Get("common.disconnected") : svcLabel)}";
        SidebarServiceStatus.Text = ServiceTopText.Text;
        ServiceDot.Fill = (svc is "connected" or "conectado")
            ? (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
            : (svc is "rejected" or "rechazado" ? (Brush)Application.Current.Resources["SystemFillColorCautionBrush"] : (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]);
        UpdateServicePulse(svc is "connected" or "conectado");

        // System health summary
        if (ctx is null)
        {
            SystemTopText.Text = $"{Localizer.Get("common.systemStatus")}: —";
            SidebarSystemStatus.Text = SystemTopText.Text;
        }
        else
        {
            var thermal = ctx.ThermalState == ThermalState.Throttling ? Localizer.Get("common.warning") : Localizer.Get("common.healthy");
            // Solo aviso de reinicio cuando es significativo (Windows Update o CBS).
            // Un rename benigno aislado no debe dejar "Atención: reinicio pendiente" permanente.
            var reboot = IsSignificantPendingReboot(ctx) ? $" · {Localizer.Get("common.warning")}: reinicio pendiente" : "";
            SystemTopText.Text = $"{Localizer.Get("common.systemStatus")}: {thermal}{reboot}";
            SidebarSystemStatus.Text = SystemTopText.Text;
        }

        // Operation indicator (Phase 13) — driven by OptimizePage via AppServices.State if needed.
        if (state.Recommendations.Count > 0 && state.LastAnalysisUtc is not null)
        {
            var rec = state.Recommendations.Count(r => r.Bucket == RecommendationBucket.Recommended);
            OperationTopText.Text = rec > 0 ? $"{rec} recomendadas" : "";
        }

        // Insignia ops en Optimizar + versión + elevación UAC.
        try
        {
            var badgeCount = state.Recommendations.Count(r => r.Bucket == RecommendationBucket.Recommended);
            NavOptimizeBadge.Value = badgeCount;
            NavOptimizeBadge.Visibility = badgeCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { }
        try { SidebarVersionText.Text = $"v{CAO.Shared.AppVersion.Semantic}"; } catch { }
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var elevated = new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            UacStatusText.Text = elevated ? "Admin (Elevado)" : "Estándar";
        }
        catch { UacStatusText.Text = "Admin (Elevado)"; }
    }

    private Storyboard? _servicePulse;

    /// <summary>Pulso suave del punto verde mientras el servicio está conectado.</summary>
    private void UpdateServicePulse(bool connected)
    {
        try
        {
            if (!Accessibility.ReducedMotion.ShouldAnimate) connected = false;
            if (connected)
            {
                if (_servicePulse is not null) return;
                ServiceDot.RenderTransformOrigin = new Point(0.5, 0.5);
                var transform = new CompositeTransform();
                ServiceDot.RenderTransform = transform;
                var board = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
                var scaleX = new DoubleAnimation { From = 1.0, To = 1.35, Duration = TimeSpan.FromSeconds(1.1) };
                var scaleY = new DoubleAnimation { From = 1.0, To = 1.35, Duration = TimeSpan.FromSeconds(1.1) };
                var fade = new DoubleAnimation { From = 1.0, To = 0.65, Duration = TimeSpan.FromSeconds(1.1) };
                Storyboard.SetTarget(scaleX, transform);
                Storyboard.SetTargetProperty(scaleX, "ScaleX");
                Storyboard.SetTarget(scaleY, transform);
                Storyboard.SetTargetProperty(scaleY, "ScaleY");
                Storyboard.SetTarget(fade, ServiceDot);
                Storyboard.SetTargetProperty(fade, "Opacity");
                board.Children.Add(scaleX);
                board.Children.Add(scaleY);
                board.Children.Add(fade);
                _servicePulse = board;
                board.Begin();
            }
            else if (_servicePulse is not null)
            {
                _servicePulse.Stop();
                _servicePulse = null;
                ServiceDot.RenderTransform = null;
                ServiceDot.Opacity = 1;
            }
        }
        catch { }
    }

    private static bool IsSignificantPendingReboot(SystemContext? ctx) =>
        ctx?.PendingReboot == true && ctx.PendingRebootReasons.Any(r =>
            r.Contains("Windows Update", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Component Based Servicing", StringComparison.OrdinalIgnoreCase));

    private void TrySetWindowIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");
            if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        }
        catch { }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            // Diferido: jamás compite con el pintado inicial ni con el análisis.
            await Task.Delay(TimeSpan.FromSeconds(20));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var release = await Helpers.AppUpdater.CheckAsync(cts.Token);
            if (release is null) return;
            var uiState = AppHost.Resolve<ViewModels.UiState>();
            uiState.LatestVersion = release.Tag;
            uiState.LatestAssetUrl = release.ZipUrl ?? string.Empty;
            uiState.UpdateAvailable = true;
        }
        catch { }
    }

    private async Task ProbeServiceAsync()
    {
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var pipe = AppHost.Resolve<PrivilegedPipeClient>();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var resp = await pipe.DetectAsync("disable-transparency", cts.Token);
            uiState.ServiceStatus = resp is { Accepted: true } ? "connected" : "rejected";
            uiState.ServiceCheckedUtc = DateTime.UtcNow;
        }
        catch
        {
            uiState.ServiceStatus = "unavailable";
            uiState.ServiceCheckedUtc = DateTime.UtcNow;
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

    /// <summary>Navega a Analizar y ejecuta allí el análisis completo.</summary>
    public async Task GoAnalyzeAndRunAsync()
    {
        SelectRoute("analyze");
        for (int i = 0; i < 20 && ContentFrame.Content is not Pages.AnalyzePage; i++)
            await Task.Delay(100);
        if (ContentFrame.Content is Pages.AnalyzePage page)
            await page.RunFullAnalysisAsync();
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        // Puerta de entrada: sin primer análisis solo se puede estar en Panel o Analizar.
        if (NeedsInitialAnalysis() && tag is not ("analyze" or "dashboard"))
        {
            SelectRoute("analyze");
            _ = PromptInitialAnalysisAsync();
            return;
        }

        var pageType = Navigation.RouteTable.Resolve(tag);
        if (pageType is not null)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
