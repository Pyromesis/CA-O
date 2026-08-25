using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Core.Engine;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Dashboard (spec 78): analyze-first. Shows measured hardware facts,
/// recommendation bucket counts and findings. Never mutates the system.
/// </summary>
public sealed partial class DashboardPage : Page
{
    private sealed record FindingRow(string SeverityLabel, string MessageEs);

    public DashboardPage()
    {
        InitializeComponent();
        ApplyTexts();
        RenderState();
        AppServices.State.PropertyChanged += (_, __) =>
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, RenderState);
    }

    private void ApplyTexts()
    {
        TitleText.Text = Localizer.Get("nav.dashboard");
        AnalyzeButton.Content = Localizer.Get("dashboard.analyze");
        RecommendedLabel.Text = Localizer.Get("dashboard.recommended");
        OptionalLabel.Text = Localizer.Get("dashboard.optional");
        ExperimentalLabel.Text = Localizer.Get("dashboard.experimental");
        SecurityLabel.Text = Localizer.Get("dashboard.securitySensitive");
        NotApplicableLabel.Text = Localizer.Get("dashboard.notApplicable");
        BucketsHeader.Text = "Recomendaciones";
        FindingsHeader.Text = Localizer.Get("dashboard.findings");
        HardwareHeader.Text = "Hardware y sistema";
        NoClaimsNote.Text = Localizer.Get("common.noClaims");
        var when = AppServices.State.LastAnalysisUtc?.ToLocalTime().ToString("g") ?? Localizer.Get("dashboard.never");
        LastAnalysisText.Text = $"{Localizer.Get("dashboard.lastAnalysis")}: {when}";
    }

    private void RenderState()
    {
        ApplyTexts();

        var recommendations = AppServices.State.Recommendations;
        RecommendedCount.Text = recommendations.Count(r => r.Bucket == RecommendationBucket.Recommended).ToString();
        OptionalCount.Text = recommendations.Count(r => r.Bucket == RecommendationBucket.Optional).ToString();
        ExperimentalCount.Text = recommendations.Count(r => r.Bucket == RecommendationBucket.Experimental).ToString();
        SecurityCount.Text = recommendations.Count(r => r.Bucket == RecommendationBucket.SecuritySensitive).ToString();
        NotApplicableCount.Text = recommendations.Count(r => r.Bucket == RecommendationBucket.NotApplicable).ToString();

        var context = AppServices.State.Context;
        ThermalBar.IsOpen = context?.ThermalState == ThermalState.Throttling;

        if (context is not null)
        {
            SystemSummary.Text =
                $"{context.WindowsEdition} build {context.WindowsBuild} ({context.Architecture})\n" +
                $"CPU: {context.CpuName} — {context.CpuCores} núcleos / {context.CpuLogicalProcessors} hilos\n" +
                $"RAM: {context.RamGb} GB | SSD: {(context.HasSsd ? "sí" : "no detectado")} | Portátil: {(context.IsLaptop ? "sí" : "no")}\n" +
                $"GPU: {(string.IsNullOrWhiteSpace(context.GpuName) ? "desconocida" : $"{context.GpuName} (driver {context.GpuDriverVersion})")}\n" +
                $"Seguridad: SecureBoot={Format(context.SecureBootEnabled)}, VBS={Format(context.VbsEnabled)}, HVCI={Format(context.HvciEnabled)}\n" +
                $"Anti-cheats detectados: {(context.AntiCheats.Count == 0 ? "ninguno" : string.Join(", ", context.AntiCheats.Select(a => a.Kind)))}";
        }
    }

    private static string Format(bool? value) => value is null ? "desconocido" : value.Value ? "activado" : "desactivado";

    private async void OnAnalyzeClick(object sender, RoutedEventArgs e)
    {
        AnalyzeButton.IsEnabled = false;
        AnalyzingRing.IsActive = true;
        try
        {
            var context = await AppServices.ContextProvider.GetAsync();
            AppServices.State.Context = context;

            await Task.Run(() =>
            {
                var recommendations = RecommendationEngine.BuildAll(AppServices.Catalog, AppServices.Registry, context);
                AppServices.State.Recommendations = recommendations;
            });

            var report = Core.Engine.SystemHealthAnalyzer.Analyze(
                new Core.Abstractions.SystemInfoReport(
                    WindowsVersion: $"build {context.WindowsBuild}",
                    WindowsEdition: context.WindowsEdition,
                    RamGb: context.RamGb,
                    CpuName: context.CpuName,
                    HasSsd: context.HasSsd,
                    IsElevated: false,
                    IsLaptop: context.IsLaptop)
                { WindowsBuild = context.WindowsBuild });

            var rows = report.Findings.Select(finding => new FindingRow(finding.Severity.ToString(), finding.MessageEs)).ToList();
            if (context.ThermalState == ThermalState.Throttling)
            {
                rows.Insert(0, new FindingRow("Critical", "Throttling térmico activo: priorice refrigeración antes de optimizar."));
            }
            FindingsList.ItemsSource = rows;

            AppServices.State.LastAnalysisUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            SystemSummary.Text = $"El análisis falló: {ex.Message}";
        }
        finally
        {
            AnalyzingRing.IsActive = false;
            AnalyzeButton.IsEnabled = true;
        }
    }
}
