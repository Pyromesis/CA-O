using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Gaming;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Gaming page (spec 27-29, 59-60): anti-cheat detection with conservative
/// messaging and vendor-native latency tech guidance (Reflex / Anti-Lag).
/// CA-O never replaces Reflex/Anti-Lag with registry hacks.
/// </summary>
public sealed partial class GamingPage : Page
{
    public GamingPage()
    {
        InitializeComponent();
        ScanButton.Content = "Escanear entorno gaming";
    }

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        ScanButton.IsEnabled = false;
        Ring.IsActive = true;
        try
        {
            var context = AppServices.State.Context ?? await AppServices.ContextProvider.GetAsync();
            AppServices.State.Context = context;

            var antiCheats = new AntiCheatScanProvider().Scan();
            AntiCheatText.Text = antiCheats.Count == 0
                ? "No se han detectado servicios/drivers de anti-cheat conocidos."
                : string.Join("\n", antiCheats.Select(cheat =>
                    $"• {cheat.Kind}: {string.Join(", ", cheat.Components)}"));

            VanguardBar.IsOpen = context.VanguardDetected;

            GpuText.Text =
                $"GPU: {(string.IsNullOrWhiteSpace(context.GpuName) ? "desconocida" : context.GpuName)}\n" +
                $"Driver: {(string.IsNullOrWhiteSpace(context.GpuDriverVersion) ? "desconocido" : context.GpuDriverVersion)}\n" +
                $"Refresco del monitor principal: {(context.DisplayRefreshHz > 0 ? $"{context.DisplayRefreshHz} Hz" : "desconocido")}\n" +
                $"Estado térmico: {context.ThermalState}";

            VendorText.Text = context.GpuVendor switch
            {
                "NVIDIA" =>
                    "NVIDIA Reflex (y Reflex 2 con Frame Warp) es la vía correcta para reducir latencia del sistema cuando el juego lo soporta; " +
                    "tiene prioridad sobre ajustes globales como Ultra Low Latency Mode. Configure Reflex dentro del juego y mantenga el driver actualizado.",
                "AMD" =>
                    "AMD Anti-Lag / Anti-Lag 2 e HYPR-RX se controlan desde el driver Adrenalin o el propio juego. " +
                    "CA-O no aplica equivalentes de registro: las tecnologías nativas del fabricante tienen prioridad.",
                _ => "Use el panel del fabricante de su GPU para las tecnologías de latencia nativas (Reflex, Anti-Lag). No existen hacks de registro fiables que las sustituyan.",
            };
        }
        catch (Exception ex)
        {
            AntiCheatText.Text = $"El escaneo falló: {ex.Message}";
        }
        finally
        {
            Ring.IsActive = false;
            ScanButton.IsEnabled = true;
        }
    }
}
