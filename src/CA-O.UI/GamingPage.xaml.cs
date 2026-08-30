using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Gaming;
using CAO.Shared;
using CAO.UI.Helpers;

namespace CAO.UI.Pages;

/// <summary>
/// Gaming page (spec 27-29, 59-60): anti-cheat detection with conservative
/// messaging and vendor-native latency tech guidance (Reflex / Anti-Lag).
/// CA-O never replaces Reflex/Anti-Lag with registry hacks.
/// </summary>
public sealed partial class GamingPage : Page
{
    private readonly ViewModels.GamingViewModel _vm;

    public GamingPage()
    {
        InitializeComponent();
        _vm = AppHost.Resolve<ViewModels.GamingViewModel>();
        DataContext = _vm;
        ApplyTexts();
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.GamingViewModel.AntiCheats) or nameof(ViewModels.GamingViewModel.GameProfiles) or nameof(ViewModels.GamingViewModel.GpuSummary) or nameof(ViewModels.GamingViewModel.VendorGuidance))
                DispatcherQueue.TryEnqueue(RenderVm);
        };
    }

    private void ApplyTexts()
    {
        ScanButton.Content = Localizer.Get("gaming.scan");
        try { LocalizationHelper.LocalizeTree(this.Content as DependencyObject ?? this); } catch { }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyTexts();
    }

    private void RenderVm()
    {
        AntiCheatText.Text = _vm.AntiCheats.Count == 0
            ? "No se han detectado servicios/drivers de anti-cheat conocidos."
            : string.Join("\n", _vm.AntiCheats.Select(cheat => $"• {cheat.Kind}: {string.Join(", ", cheat.Components)}"));
        VanguardBar.IsOpen = _vm.VanguardDetected;
        GameProfilesText.Text = _vm.GameProfiles.Count == 0
            ? "Sin juegos conocidos en ejecución/detección ahora mismo."
            : string.Join("\n\n", _vm.GameProfiles.Select(game =>
                $"▸ {game.DisplayName} ({game.Launcher})\n" +
                $"  Anti-cheat: {game.AntiCheatPolicy}\n" +
                string.Join("\n", game.GuidanceEs.Select(line => "  - " + line))));
        GpuText.Text = _vm.GpuSummary;
        VendorText.Text = _vm.VendorGuidance;
        if (GamingStatusText is not null) GamingStatusText.Text = _vm.Status;
    }

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        ScanButton.IsEnabled = false;
        Ring.IsActive = true;
        if (GamingStatusText is not null) GamingStatusText.Text = "Escaneando…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await _vm.ScanCommand.ExecuteAsync(null);
            RenderVm();
        }
        catch (Exception ex)
        {
            AntiCheatText.Text = $"{ErrorCodes.UiGamingScanFailed}: El escaneo gaming no pudo completarse. [Técnico: {ex.GetType().Name}]";
            if (GamingStatusText is not null) GamingStatusText.Text = $"{ErrorCodes.UiGamingScanFailed}: escaneo fallido";
            App.WriteCrashLog(ex);
        }
        finally
        {
            Ring.IsActive = false;
            ScanButton.IsEnabled = true;
            if (GamingStatusText is not null && GamingStatusText.Text == "Escaneando…") GamingStatusText.Text = "Escaneo completo.";
        }
    }
}
