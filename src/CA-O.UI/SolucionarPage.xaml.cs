using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Shared.IPC;

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
        "restart-desktop-compositor" => VideoStatusText,
        "clear-icon-thumbnail-cache" => VideoStatusText,
        _ => SystemStatusText,
    };

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

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.ApplyAsync(optimizationId, cts.Token);
            var message = response is { Accepted: true }
                ? "✓ Solucionado."
                : $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
            if (target != null) target.Text = $"{optimizationId}: {message}";
        }
        catch (Exception ex)
        {
            if (target != null) target.Text = $"{optimizationId}: servicio no disponible ({ex.Message})";
            App.WriteCrashLog(ex);
        }
    }
}
