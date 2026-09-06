using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Panel como centro: info del programa, accesos a las demás pestañas y
/// estado global. La salud del sistema (CPU/GPU/RAM/hallazgos) vive en Analizar.
/// </summary>
public sealed partial class DashboardPage : Page
{
    private readonly ViewModels.DashboardViewModel _vm;

    public DashboardPage()
    {
        InitializeComponent();
        _vm = AppHost.Resolve<ViewModels.DashboardViewModel>();
        DataContext = _vm;
        ApplyTexts();
        RenderHub();
        var uiState0 = AppHost.Resolve<ViewModels.UiState>();
        uiState0.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.DashboardViewModel.Recommendations) or nameof(ViewModels.DashboardViewModel.StatusMessage))
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, RenderHub);
        };
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.UiState.Context) or nameof(ViewModels.UiState.Recommendations) or nameof(ViewModels.UiState.LastAnalysisUtc) or nameof(ViewModels.UiState.ServiceStatus) or nameof(ViewModels.UiState.UpdateAvailable) or nameof(ViewModels.UiState.LatestVersion))
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, RenderHub);
        };
        Loaded += async (_, __) =>
        {
            Helpers.UiAnimations.PlayEntrance(PageContent);
            RenderHub();
            if (uiState.Context is null)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await _vm.LoadCommand.ExecuteAsync(null);
                    RenderHub();
                }
                catch (Exception ex) { App.WriteCrashLog(ex); }
            }
            ServiceInfoBar.IsOpen = uiState.ServiceStatus is not ("connected" or "conectado");
        };
    }

    private void ApplyTexts()
    {
        TitleText.Text = Localizer.Get("nav.dashboard");
        AnalyzeButton.Content = Localizer.Get("dashboard.analyze");
        ShortcutsHeader.Text = Localizer.Get("dashboard.shortcuts");
        StatusHeader.Text = Localizer.Get("dashboard.status");
        NoClaimsNote.Text = Localizer.Get("dashboard.noClaims");
        GoOptimizeButton.Content = Localizer.Get("dashboard.goOptimize");
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var when = uiState.LastAnalysisUtc?.ToLocalTime().ToString("g") ?? Localizer.Get("dashboard.never");
        LastAnalysisText.Text = $"{Localizer.Get("dashboard.lastAnalysis")}: {when}";
        AnalyzeStatusText.Text = "";
        try { Helpers.LocalizationHelper.LocalizeTree(this.Content as Microsoft.UI.Xaml.DependencyObject ?? this); } catch { }
    }

    private void RenderHub()
    {
        ApplyTexts();

        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var recommendations = uiState.Recommendations;
        var recCount = recommendations.Count(r => r.Bucket == RecommendationBucket.Recommended);

        if (recommendations.Count == 0)
            NextStepText.Text = "Ejecute “Analizar sistema” para generar recomendaciones clasificadas por evidencia y riesgo.";
        else if (recCount > 0)
            NextStepText.Text = $"Hay {recCount} cambios recomendados listos para revisar — cada uno con diff previo y rollback.";
        else
            NextStepText.Text = "No hay recomendados pendientes. Revise opcionales/experimentales en Modo Expert si lo necesita.";
        TileOptimizeSub.Text = recCount > 0 ? $"{recCount} recomendadas" : "Sin recomendaciones";

        // Programa
        ProgramInfoText.Text = $"CA-O {CAO.Shared.AppVersion.Semantic} · Protocolo IPC v{CAO.Shared.IPC.IpcProtocol.Version} · " +
            $"{CAO.Core.Catalog.OptimizationCatalog.All.Count} optimizaciones";
        AboutText.Text = $"Versión {CAO.Shared.AppVersion.Semantic}\n" +
            $"Ajustes: {CAO.Shared.CaOPaths.SettingsFile}\n" +
            $"Sin telemetría externa. Sin promesas numéricas: solo mediciones.";

        // Servicio
        var svc = uiState.ServiceStatus ?? "unknown";
        bool connected = svc is "connected" or "conectado";
        ServiceBadgeText.Text = connected
            ? $"{Localizer.Get("common.serviceStatus")}: {Localizer.Get("common.connected")}"
            : $"{Localizer.Get("common.serviceStatus")}: {Localizer.Get("common.disconnected")}";
        ServiceBadge.Background = connected
            ? (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
            : (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];

        // Avisos
        var context = uiState.Context;
        ThermalBar.IsOpen = context?.ThermalState == ThermalState.Throttling;
        var isSignificantReboot = context?.PendingReboot == true && context.PendingRebootReasons.Any(r => r.Contains("Windows Update", StringComparison.OrdinalIgnoreCase) || r.Contains("Component Based Servicing", StringComparison.OrdinalIgnoreCase));
        PendingRebootBar.IsOpen = isSignificantReboot;
        if (isSignificantReboot)
            PendingRebootBar.Message = "Reinicio pendiente por: " + string.Join(", ", context!.PendingRebootReasons) + ".";
        RecoveryBar.IsOpen = uiState.RecoveryCandidates.Count > 0;
        ServiceInfoBar.IsOpen = !connected;
        UpdateBar.IsOpen = uiState.UpdateAvailable && !string.IsNullOrWhiteSpace(uiState.LatestVersion);
        if (UpdateBar.IsOpen)
            UpdateBar.Message = $"Hay una versión nueva de CA-O ({uiState.LatestVersion}).";
    }

    private async void OnAnalyzeClick(object sender, RoutedEventArgs e)
    {
        // El botón ejecuta el análisis completo dentro de Analizar.
        AnalyzeButton.IsEnabled = false;
        AnalyzingRing.IsActive = true;
        AnalyzeStatusText.Text = Localizer.Get("dashboard.analyzing");
        try
        {
            if (MainWindow.Current is not null)
                await MainWindow.Current.GoAnalyzeAndRunAsync();
            else
                AppHost.Resolve<Navigation.INavigationService>().Select("analyze");
        }
        catch (Exception ex)
        {
            AnalyzeStatusText.Text = "No se pudo iniciar el análisis.";
            App.WriteCrashLog(ex);
        }
        finally
        {
            AnalyzingRing.IsActive = false;
            AnalyzeButton.IsEnabled = true;
            if (AnalyzeStatusText.Text == Localizer.Get("dashboard.analyzing")) AnalyzeStatusText.Text = "";
        }
    }

    private void OnTileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        if (MainWindow.Current != null) MainWindow.Current.SelectRoute(tag);
        else
        {
            var nav = AppHost.Resolve<Navigation.INavigationService>();
            nav.Select(tag);
        }
    }

    private void OnGoOptimizeClick(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Current != null) MainWindow.Current.SelectRoute("optimize");
        else
        {
            var nav = AppHost.Resolve<Navigation.INavigationService>();
            nav.Select("optimize");
        }
    }

    private void OnGoUpdateClick(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Current != null) MainWindow.Current.SelectRoute("settings");
        else
        {
            var nav = AppHost.Resolve<Navigation.INavigationService>();
            nav.Select("settings");
        }
    }
}
