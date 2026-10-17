using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.UI.Helpers;
using CAO.Shared.IPC;
using CAO.UI.Controls;

namespace CAO.UI.Pages;

/// <summary>Solucionadores directos: audio, video y sistema, con efecto real.</summary>
public sealed partial class SolucionarPage : Page
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
        MainWindow.Current?.StartPageTour("solucionar");
    }

    public SolucionarPage()
    {
        try { InitializeComponent(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SolucionarPage init failed: {ex}"); throw; }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyPageTourText();
        Helpers.UiAnimations.PlayEntrance(PageContent);
    }

    private async void OnFixClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        await RunFixAsync(id, StatusBoxFor(id), confirm: false);
    }

    private async void OnFixConfirmClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        var dialog = new ContentDialog
        {
            Title = "Confirmar solución",
            Content = ConfirmTextFor(id),
            PrimaryButtonText = "Ejecutar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await UiDialogs.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        await RunFixAsync(id, StatusBoxFor(id), confirm: false);
    }

    private static string ConfirmTextFor(string id) => id switch
    {
        "renew-ip-address" or "restart-wlan-service" =>
            "La conexión se corta unos segundos y vuelve sola. ¿Continuar?",
        "reset-network-stack-repair" =>
            "Restablece Winsock y TCP/IP: se pierden configuraciones de red personalizadas (IP fija, proxies de sistema) y hay que reiniciar el equipo. Úsalo solo si lo demás no funcionó. ¿Continuar?",
        "run-system-file-checker" =>
            "SFC revisa todos los archivos de Windows y tarda de 5 a 20 minutos. Puedes seguir usando el equipo, pero no lo apagues. ¿Continuar?",
        "repair-windows-image" =>
            "DISM descarga de Windows Update lo necesario y tarda de 10 a 30 minutos. Necesita internet. ¿Continuar?",
        "restart-xbox-services" =>
            "Cierra antes los juegos de Xbox o Game Pass abiertos. ¿Continuar?",
        "rebuild-font-cache" =>
            "Algunas apps abiertas pueden mostrar texto raro hasta reiniciarlas. ¿Continuar?",
        _ => "Es seguro pero interrumpe unos segundos (pantalla o red según el caso). ¿Continuar?",
    };

    private TextBlock? StatusBoxFor(string id) => id switch
    {
        "restart-windows-audio-services" => AudioStatusText,
        "fix-microphone-access" => AudioStatusText,
        "disable-bluetooth-absolute-volume" => AudioStatusText,
        "restart-bluetooth-service" => AudioStatusText,
        "restart-desktop-compositor" => VideoStatusText,
        "clear-icon-thumbnail-cache" => VideoStatusText,
        "rebuild-font-cache" => VideoStatusText,
        "flush-dns-cache" or "restart-dns-client" or "renew-ip-address" or "clear-arp-cache"
            or "restart-wlan-service" or "reset-network-stack-repair" => NetworkStatusText,
        "restart-xbox-services" or "restart-text-input-service" => GamesFixStatusText,
        "restart-print-spooler" => PrintStatusText,
        _ => SystemStatusText,
    };

    private static bool IsExplorerFix(string id) =>
        id.Equals("restart-windows-explorer", StringComparison.Ordinal) ||
        id.Equals("recover-windows-explorer", StringComparison.Ordinal);

    private async Task RunFixAsync(string optimizationId, TextBlock? target, bool confirm)
    {
        if (confirm)
        {
            var dialog = new ContentDialog
            {
                Title = "Confirmar solución",
                Content = $"Se ejecutará '{optimizationId}'. ¿Continuar?",
                PrimaryButtonText = "Ejecutar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (await UiDialogs.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        }

        Mascot.Set("Working");
        ShowFixProgress($"Aplicando {optimizationId}…");
        try
        {
            // SFC y DISM tardan hasta 30 min: con 120 s la UI abandonaba mientras el
            // servicio seguía reparando y el usuario veía un error falso.
            using var cts = new CancellationTokenSource(CAO.Shared.TimeoutProfile.HeavyOptimizationIds.Contains(optimizationId)
                ? CAO.Shared.TimeoutProfile.DispatchHeavy
                : TimeSpan.FromSeconds(120));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.ApplyAsync(optimizationId, cts.Token);
            if (response is { Accepted: true })
            {
                if (target != null) target.Text = $"{optimizationId}: ✓ Solucionado.";
                ShowFixProgress($"✓ {optimizationId} solucionado.");
                Mascot.CelebrateThenIdle(DispatcherQueue);
                try { await Task.Delay(1500); } catch { }
                HideFixProgress();
                return;
            }
            var message = $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
            // Plan B local para el shell: la UI vive en la sesión del usuario
            // y puede relanzar explorer.exe sin el servicio (p. ej. servicio
            // desactualizado sin este id, o pipe saturado).
            if (IsExplorerFix(optimizationId))
            {
                var local = await Helpers.ExplorerRecovery.RecoverLocallyAsync(
                    killLeftovers: optimizationId.Equals("restart-windows-explorer", StringComparison.Ordinal),
                    cts.Token);
                message += $"\nFallback local: {local}";
            }
            if (target != null) target.Text = $"{optimizationId}: {message}";
            ShowFixProgress($"{optimizationId}: {message}");
            Mascot.Set("Warn");
            try { await Task.Delay(1500); } catch { }
            HideFixProgress();
        }
        catch (Exception ex)
        {
            var message = $"servicio no disponible ({ex.Message})";
            if (IsExplorerFix(optimizationId))
            {
                // Sin cts aquí (está disposed tras el using): token con techo
                // propio para que el fallback no cuelgue la UI sin límite.
                using var fallbackCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var local = await Helpers.ExplorerRecovery.RecoverLocallyAsync(
                    killLeftovers: optimizationId.Equals("restart-windows-explorer", StringComparison.Ordinal),
                    fallbackCts.Token);
                message += $"\nFallback local: {local}";
            }
            if (target != null) target.Text = $"{optimizationId}: {message}";
            App.WriteCrashLog(ex);
            ShowFixProgress($"{optimizationId}: {message}");
            Mascot.Set("Warn");
            try { await Task.Delay(1500); } catch { }
            HideFixProgress();
        }
    }

    /// <summary>Muestra la tarjeta global de progreso. Nunca lanza.</summary>
    private void ShowFixProgress(string text)
    {
        try { FixProgressCard.Visibility = Visibility.Visible; FixRing.IsActive = true; FixProgressText.Text = text; FixProgressBar.IsIndeterminate = true; } catch { }
    }

    private void HideFixProgress()
    {
        try { FixProgressCard.Visibility = Visibility.Collapsed; FixRing.IsActive = false; } catch { }
    }
}
