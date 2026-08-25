using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Networking;
using CAO.Infrastructure.Security;
using CAO.Infrastructure.Storage;
using CAO.Infrastructure.Input;
using CAO.Infrastructure.SystemInterop;

namespace CAO.UI;

public sealed partial class MainWindow : Window
{
    private readonly PrivilegedPipeClient _pipe = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void AnalyzeCurrentState(object sender, RoutedEventArgs args)
    {
        if (sender is Button button) button.IsEnabled = false;
        ConnectionStatus.Text = "Service status: connecting...";
        try
        {
            var system = await new WmiSystemInfoProvider().GetAsync();
            SystemSummary.Text = $"System: {system.CpuName} | {system.CpuCores} cores / {system.CpuLogicalProcessors} threads | RAM {system.RamGb} GB | {system.Architecture} | build {system.WindowsBuild}";

            var network = await new NetworkDiagnosticsProvider().MeasureAsync();
            var measurements = network.Measurements
                .Select(measurement => $"{measurement.Kind} {measurement.Endpoint}: " +
                    (measurement.MedianLatencyMs is null ? "unreachable" : $"{measurement.MedianLatencyMs:0.0} ms, loss {measurement.Attempts - measurement.SuccessfulAttempts}/{measurement.Attempts}"));
            NetworkSummary.Text = $"Network: {string.Join("; ", network.Interfaces)}\n{string.Join("\n", measurements)}";

            var security = new SecurityDiagnosticsProvider().Measure();
            var securityFeatures = security.Features.Select(feature =>
                $"{feature.Name}: {(feature.Enabled is null ? "unknown" : feature.Enabled.Value ? "enabled" : "disabled")}");
            SecuritySummary.Text = $"Security: {string.Join(", ", securityFeatures)} | Vanguard: {(security.VanguardDetected ? "detected" : "not detected")}";

            var storage = new StorageDiagnosticsProvider().Measure();
            StorageSummary.Text = "Storage: " + string.Join("; ", storage.Volumes.Select(volume =>
                $"{volume.Name} {volume.FileSystem}, free {volume.FreeBytes / (1024d * 1024 * 1024):0.0} / {volume.TotalBytes / (1024d * 1024 * 1024):0.0} GB"));

            var drivers = await new DriverDiagnosticsProvider().MeasureAsync();
            var driverIssues = drivers.Drivers.Count(driver => driver.ProblemCode != 0 || driver.IsSigned == false);
            DriverSummary.Text = $"Drivers: {drivers.Drivers.Count} detected, {driverIssues} with a problem code or unsigned state.";

            var performance = await new PerformanceDiagnosticsProvider().MeasureAsync();
            var processorSummary = string.Join("; ", performance.Processors.Select(processor =>
                $"CPU {processor.LoadPercent?.ToString() ?? "unknown"}% @ {processor.CurrentClockMHz?.ToString() ?? "unknown"} MHz"));
            var graphicsSummary = string.Join("; ", performance.GraphicsAdapters.Select(adapter =>
                $"GPU {adapter.Name} ({adapter.DriverVersion})"));
            PerformanceSummary.Text = $"Performance: {processorSummary}\n{graphicsSummary}\nThermals: sensor data not available in this read-only WMI pass.";

            var input = await new InputDiagnosticsProvider().MeasureAsync();
            InputSummary.Text = $"Input: mouse acceleration {(input.MouseAccelerationEnabled is null ? "unknown" : input.MouseAccelerationEnabled.Value ? "enabled" : "disabled")}; HID devices {input.HidDeviceCount}. No input content is captured.";

            var thermals = await new ThermalDiagnosticsProvider().MeasureAsync();
            ThermalSummary.Text = thermals.IsAvailable
                ? $"Thermals: {string.Join(", ", thermals.Zones.Select(zone => $"{zone.Name} {zone.TemperatureCelsius:0.0} C"))} (ACPI zones; CPU/GPU sensors may require vendor APIs)."
                : "Thermals: unknown; this system did not expose readable ACPI zones.";

            var response = await _pipe.DetectAsync("disable-vbs");
            ConnectionStatus.Text = "Service status: connected";
            AnalysisResult.Text = response is { Accepted: true }
                ? $"Disable VBS: {response.MessageEs}"
                : $"No change executed: {response?.Error ?? "No response."}";
        }
        catch (Exception ex)
        {
            ConnectionStatus.Text = "Service status: unavailable";
            AnalysisResult.Text = $"The privileged service could not be reached: {ex.Message}";
        }
        finally
        {
            if (sender is Button completedButton) completedButton.IsEnabled = true;
        }
    }
}