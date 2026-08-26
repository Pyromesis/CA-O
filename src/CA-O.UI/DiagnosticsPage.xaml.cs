using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Input;
using CAO.Infrastructure.SystemInterop;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Hardware diagnostics (spec 18, 30-35): input, thermal and live
/// performance snapshots. Read-only; no keystroke or pointer content is ever
/// captured (spec 116).
/// </summary>
public sealed partial class DiagnosticsPage : Page
{
    public DiagnosticsPage()
    {
        InitializeComponent();
        RunButton.Content = Localizer.Get("analyze.run");
        if (StatusText is not null) StatusText.Text = "";
    }

    private async void OnRunClick(object sender, RoutedEventArgs e)
    {
        RunButton.IsEnabled = false;
        Ring.IsActive = true;
        if (StatusText is not null) StatusText.Text = "Midiendo…";
        try
        {
            var input = await new InputDiagnosticsProvider().MeasureAsync();
            InputText.Text =
                $"Aceleración del ratón: {(input.MouseAccelerationEnabled is null ? "desconocida" : input.MouseAccelerationEnabled.Value ? "activada" : "desactivada")} · " +
                $"Dispositivos HID: {input.HidDeviceCount}\n" +
                "No se captura contenido de entrada: sólo estado y temporización de dispositivos.";

            var thermals = await new ThermalDiagnosticsProvider().MeasureAsync();
            ThermalText.Text = thermals.IsAvailable
                ? string.Join("\n", thermals.Zones.Select(zone =>
                    $"• {zone.Name}: {(zone.TemperatureCelsius is null ? "sin lectura" : $"{zone.TemperatureCelsius:0.0} °C")} ({zone.Source})"))
                : "Este sistema no expone zonas térmicas ACPI legibles; los sensores CPU/GPU requieren APIs del fabricante.";

            var performance = await new PerformanceDiagnosticsProvider().MeasureAsync();
            PerfText.Text =
                string.Join("; ", performance.Processors.Select(processor =>
                    $"CPU {processor.LoadPercent?.ToString("0") ?? "?"}% @ {processor.CurrentClockMHz?.ToString("0") ?? "?"} MHz")) +
                "\n" +
                string.Join("\n", performance.GraphicsAdapters.Select(adapter =>
                    $"GPU: {adapter.Name} (driver {adapter.DriverVersion})"));
        }
        catch (Exception ex)
        {
            InputText.Text = $"{ErrorCodes.UiDiagnosticsFailed}: No fue posible completar el diagnóstico. Reintente tras cerrar otras herramientas de monitorización. [Técnico: {ex.GetType().Name}]";
            if (StatusText is not null) StatusText.Text = $"{ErrorCodes.UiDiagnosticsFailed}: diagnóstico no completado";
            App.WriteCrashLog(ex);
        }
        finally
        {
            Ring.IsActive = false;
            RunButton.IsEnabled = true;
            if (StatusText is not null && StatusText.Text == "Midiendo…") StatusText.Text = "Diagnóstico completo.";
        }
    }
}
