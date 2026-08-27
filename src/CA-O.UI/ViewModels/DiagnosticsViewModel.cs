using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Infrastructure.Input;
using CAO.Infrastructure.SystemInterop;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para DiagnosticsPage — métricas de entrada, térmicas y rendimiento.</summary>
public sealed partial class DiagnosticsViewModel : ObservableObject
{
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _inputSummary = string.Empty;
    [ObservableProperty] private string _thermalSummary = string.Empty;
    [ObservableProperty] private string _performanceSummary = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    [RelayCommand]
    private async Task RunAsync(CancellationToken ct)
    {
        IsRunning = true;
        Status = "Midiendo…";
        try
        {
            // Paralelo cuando es independiente (§8)
            var inputTask = new InputDiagnosticsProvider().MeasureAsync(ct);
            var thermalTask = new ThermalDiagnosticsProvider().MeasureAsync(ct);
            var perfTask = new PerformanceDiagnosticsProvider().MeasureAsync(ct);

            await Task.WhenAll(inputTask, thermalTask, perfTask);

            var input = await inputTask;
            InputSummary =
                $"Aceleración ratón: {(input.MouseAccelerationEnabled is null ? "desconocida" : input.MouseAccelerationEnabled.Value ? "activada" : "desactivada")} · " +
                $"HID: {input.HidDeviceCount}\nNo se captura contenido de entrada.";

            var thermals = await thermalTask;
            ThermalSummary = thermals.IsAvailable
                ? string.Join("\n", thermals.Zones.Select(z => $"• {z.Name}: {(z.TemperatureCelsius is null ? "sin lectura" : $"{z.TemperatureCelsius:0.0} °C")} ({z.Source})"))
                : "Sin zonas térmicas ACPI legibles; sensores CPU/GPU requieren APIs del fabricante.";

            var perf = await perfTask;
            PerformanceSummary =
                string.Join("; ", perf.Processors.Select(p => $"CPU {p.LoadPercent?.ToString("0") ?? "?"}% @ {p.CurrentClockMHz?.ToString("0") ?? "?"} MHz")) + "\n" +
                string.Join("\n", perf.GraphicsAdapters.Select(a => $"GPU: {a.Name} (driver {a.DriverVersion})"));

            Status = "Diagnóstico completo.";
        }
        catch (OperationCanceledException) { Status = "Diagnóstico cancelado."; }
        catch (Exception ex)
        {
            InputSummary = $"{ErrorCodes.UiDiagnosticsFailed}: No fue posible completar el diagnóstico. [Técnico: {ex.GetType().Name}]";
            Status = $"{ErrorCodes.UiDiagnosticsFailed}: diagnóstico no completado";
            App.WriteCrashLog(ex);
        }
        finally { IsRunning = false; }
    }
}
