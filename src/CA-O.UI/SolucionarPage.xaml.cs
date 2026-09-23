using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Shared.IPC;
using CAO.UI.Controls;

namespace CAO.UI.Pages;

/// <summary>Solucionadores directos: audio, video y sistema, con efecto real.</summary>
public sealed partial class SolucionarPage : Page
{
    public SolucionarPage()
    {
        try { InitializeComponent(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SolucionarPage init failed: {ex}"); throw; }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
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
            Content = $"Se ejecutará '{id}'. Es seguro pero interrumpe unos segundos (pantalla o red según el caso). ¿Continuar?",
            PrimaryButtonText = "Ejecutar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await RunFixAsync(id, StatusBoxFor(id), confirm: false);
    }

    private TextBlock? StatusBoxFor(string id) => id switch
    {
        "restart-windows-audio-services" => AudioStatusText,
        "fix-microphone-access" => AudioStatusText,
        "disable-bluetooth-absolute-volume" => AudioStatusText,
        "restart-bluetooth-service" => AudioStatusText,
        "restart-desktop-compositor" => VideoStatusText,
        "clear-icon-thumbnail-cache" => VideoStatusText,
        "flush-dns-cache" => NetworkStatusText,
        "restart-dns-client" => NetworkStatusText,
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
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        }

        Mascot.Set("Working");
        ShowFixProgress($"Aplicando {optimizationId}…");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
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
