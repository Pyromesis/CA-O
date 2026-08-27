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
    [ObservableProperty] private string _storageSummary = string.Empty;
    [ObservableProperty] private string _securitySummary = string.Empty;
    [ObservableProperty] private string _driversSummary = string.Empty;
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
            var storageTask = Task.Run(() => new Infrastructure.Storage.StorageDiagnosticsProvider().Measure(), ct);
            var securityTask = Task.Run(() => new Infrastructure.Security.SecurityDiagnosticsProvider().Measure(), ct);
            var driversTask = new Infrastructure.SystemInterop.DriverDiagnosticsProvider().MeasureAsync(ct);

            await Task.WhenAll(inputTask, thermalTask, perfTask, driversTask);

            var input = await inputTask;
            InputSummary =
                $"Aceleración ratón: {(input.MouseAccelerationEnabled is null ? "desconocida" : input.MouseAccelerationEnabled.Value ? "activada" : "desactivada")} · " +
                $"HID: {input.HidDeviceCount}\nNo se captura contenido de entrada.";

            var thermals = await thermalTask;
            ThermalSummary = thermals.IsAvailable
                ? string.Join("\n", thermals.Zones.Select(z => $"• {z.Name}: {(z.TemperatureCelsius is null ? "sin lectura" : $"{z.TemperatureCelsius:0.0} °C")} ({z.Source})"))
                : "Sin zonas térmicas ACPI legibles; sensores CPU/GPU requieren APIs del fabricante.";

            var perf = await perfTask;
            var cpuInterpret = perf.Processors.FirstOrDefault()?.LoadPercent is int load ? (load > 85 ? "Alta carga" : load > 60 ? "Carga media" : "Normal") : "No disponible";
            PerformanceSummary =
                string.Join("; ", perf.Processors.Select(p => $"CPU {p.LoadPercent?.ToString("0") ?? "?"}% @ {p.CurrentClockMHz?.ToString("0") ?? "?"} MHz — {cpuInterpret}")) + "\n" +
                string.Join("\n", perf.GraphicsAdapters.Select(a => $"GPU: {a.Name} (driver {a.DriverVersion} — {(string.IsNullOrWhiteSpace(a.DriverVersion) ? "No disponible en este hardware/API" : "disponible")})")) +
                (perf.GraphicsAdapters.Count == 0 ? "\nGPU: No disponible en este hardware/API" : "");

            var storage = await storageTask;
            var sysVol = storage.Volumes.FirstOrDefault(v => v.IsSystemVolume);
            StorageSummary = sysVol is null ? "Disco: No disponible en este hardware/API"
                : $"Disco {sysVol.Name} ({sysVol.FileSystem}): {sysVol.FreeBytes/1024/1024/1024:0} GB libres de {sysVol.TotalBytes/1024/1024/1024:0} GB — {(sysVol.FreeBytes/(double)sysVol.TotalBytes < 0.1 ? "Atención: poco espacio" : "Normal")}";

            var sec = await securityTask;
            SecuritySummary = string.Join("\n", sec.Features.Select(f => $"• {f.Name}: {(f.Enabled is null ? "No disponible" : f.Enabled.Value ? "Activado — OK" : "Desactivado — Revisar")}")) +
                $"\nVanguard: {(sec.VanguardDetected ? "Detectado — modo protegido activo" : "No detectado")}";

            var drivers = await driversTask;
            var broken = drivers.Drivers.Count(d => d.ProblemCode != 0);
            var unsigned = drivers.Drivers.Count(d => d.IsSigned == false);
            DriversSummary = $"{drivers.Drivers.Count} drivers — {broken} con problema, {unsigned} sin firma\n" +
                (broken == 0 && unsigned == 0 ? "Estado: Normal" : broken > 0 ? "Estado: Requiere atención" : "Estado: Revisar firmas") +
                (drivers.Drivers.Count == 0 ? " — No disponible en este hardware/API" : "");

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
