using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CAO.Core.Diagnostics;
using CAO.Core.Engine;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Premium dashboard (Fase 5-7): hero health, 4-column hardware cards,
/// bucket counts, interactive findings, security posture — every state visible in <5s.
/// </summary>
public sealed partial class DashboardPage : Page
{
    private sealed record FindingRow(string SeverityLabel, string MessageEs, Brush SeverityBrush);
    private SystemDiagnosticReport? _lastReport;

    public DashboardPage()
    {
        InitializeComponent();
        ApplyTexts();
        RenderState();
        AppServices.State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.UiState.Context) or nameof(ViewModels.UiState.Recommendations) or nameof(ViewModels.UiState.LastAnalysisUtc) or nameof(ViewModels.UiState.ServiceStatus))
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, RenderState);
        };
        Loaded += async (_, __) =>
        {
            // Fast first render: if no cached context, hydrate in background without blocking UI (Fase 25)
            if (AppServices.State.Context is null)
            {
                try { AppServices.State.Context = await AppServices.ContextProvider.GetAsync(); RenderState(); } catch { }
            }
            // Reflect service availability (read-only mode if unavailable)
            ServiceInfoBar.IsOpen = AppServices.State.ServiceStatus is "unavailable" or "unknown";
        };
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
        BucketsHeader.Text = Localizer.Get("dashboard.health");
        FindingsHeader.Text = Localizer.Get("dashboard.findings");
        HardwareHeader.Text = "Hardware y sistema";
        NoClaimsNote.Text = Localizer.Get("dashboard.noClaims");
        var when = AppServices.State.LastAnalysisUtc?.ToLocalTime().ToString("g") ?? Localizer.Get("dashboard.never");
        LastAnalysisText.Text = $"{Localizer.Get("dashboard.lastAnalysis")}: {when}";
        AnalyzeStatusText.Text = "";
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

        if (recommendations.Count == 0)
            NextStepText.Text = "Ejecute “Analizar sistema” para generar recomendaciones clasificadas por evidencia y riesgo.";
        else if (recommendations.Any(r => r.Bucket == RecommendationBucket.Recommended))
            NextStepText.Text = $"Hay {recommendations.Count(r => r.Bucket == RecommendationBucket.Recommended)} cambios recomendados listos para revisar — cada uno con diff previo y rollback.";
        else
            NextStepText.Text = "No hay recomendados pendientes. Revise opcionales/experimentales en Modo Expert si lo necesita.";

        var context = AppServices.State.Context;
        ThermalBar.IsOpen = context?.ThermalState == ThermalState.Throttling;
        PendingRebootBar.IsOpen = context?.PendingReboot == true;
        if (context is not null && context.PendingReboot)
            PendingRebootBar.Message = "Reinicio pendiente por: " + string.Join(", ", context.PendingRebootReasons) + ".";
        RecoveryBar.IsOpen = AppServices.State.RecoveryCandidates.Count > 0;
        ServiceInfoBar.IsOpen = AppServices.State.ServiceStatus is "unavailable" or "unknown";

        if (context is null)
        {
            SystemSummary.Text = "Sin datos de sistema aún — pulse Analizar.";
            CpuNameText.Text = "—";
            CpuDetailText.Text = "Ejecute el análisis";
            GpuNameText.Text = "—";
            GpuDetailText.Text = "";
            GpuDriverText.Text = "";
            RamText.Text = "—";
            RamDetailText.Text = "";
            StorageSummaryText.Text = "";
            SecurityPosturePanel.Children.Clear();
            AntiCheatText.Text = "Anti-cheats: —";
            SystemHealthText.Text = "Sistema: sin datos";
            SystemHealthBadge.Background = (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];
            HealthScoresText.Text = "";
            WhyScoresButton.Visibility = Visibility.Collapsed;
            EmptyFindingsState.Visibility = Visibility.Visible;
            FindingsList.Visibility = Visibility.Collapsed;
            return;
        }

        // CPU card
        CpuNameText.Text = string.IsNullOrWhiteSpace(context.CpuName) ? "CPU desconocida" : context.CpuName;
        CpuDetailText.Text = $"{context.CpuCores} núcleos / {context.CpuLogicalProcessors} hilos · {context.Architecture} · {(context.IsLaptop ? "Portátil" : "Sobremesa")}";
        // GPU card
        GpuNameText.Text = string.IsNullOrWhiteSpace(context.GpuName) ? "GPU no detectada" : context.GpuName;
        GpuDetailText.Text = context.HasSsd ? "SSD detectado" : "SSD no detectado";
        GpuDriverText.Text = string.IsNullOrWhiteSpace(context.GpuDriverVersion) ? "" : $"Driver {context.GpuDriverVersion}";
        // RAM
        RamText.Text = $"{context.RamGb} GB";
        RamDetailText.Text = $"Windows {context.WindowsEdition} build {context.WindowsBuild}";
        StorageSummaryText.Text = context.IsLaptop ? "Modo portátil" : "Modo sobremesa";

        // Security posture (Fase 87)
        SecurityPosturePanel.Children.Clear();
        void AddPosture(string label, bool? enabled)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var dot = new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
            dot.Fill = enabled == true ? (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"] : enabled == false ? (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"] : (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];
            row.Children.Add(dot);
            row.Children.Add(new TextBlock { Text = $"{label}: {(enabled is null ? "desconocido" : enabled.Value ? "activado" : "desactivado")}", FontSize = 11, Opacity = 0.85 });
            SecurityPosturePanel.Children.Add(row);
        }
        AddPosture("Secure Boot", context.SecureBootEnabled);
        AddPosture("VBS", context.VbsEnabled);
        AddPosture("HVCI", context.HvciEnabled);
        AntiCheatText.Text = context.AntiCheats.Count == 0 ? "Anti-cheats: ninguno" : $"Anti-cheats: {string.Join(", ", context.AntiCheats.Select(a => a.Kind))}";

        // Compact system summary (secondary)
        SystemSummary.Text = $"{context.WindowsEdition} build {context.WindowsBuild} ({context.Architecture})";

        // Health scores (Fase 6: never magic number alone)
        if (_lastReport is not null)
        {
            HealthScoresText.Text = DescribeScores(_lastReport);
            WhyScoresButton.Visibility = string.IsNullOrWhiteSpace(HealthScoresText.Text) ? Visibility.Collapsed : Visibility.Visible;
            var findings = _lastReport.Findings.Select(f => new FindingRow(f.Severity.ToString(), f.MessageEs, BrushFor(f.Severity.ToString()))).ToList();
            FindingsList.ItemsSource = findings;
            FindingsList.Visibility = findings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            EmptyFindingsState.Visibility = findings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SystemHealthText.Text = DeriveSystemStatus(_lastReport);
            SystemHealthBadge.Background = BrushForStatus(SystemHealthText.Text);
        }
        else
        {
            HealthScoresText.Text = "";
            WhyScoresButton.Visibility = Visibility.Collapsed;
            if (FindingsList.ItemsSource is null)
            {
                EmptyFindingsState.Visibility = Visibility.Visible;
                FindingsList.Visibility = Visibility.Collapsed;
            }
        }
    }

    private static string Format(bool? value) => value is null ? "desconocido" : value.Value ? "activado" : "desactivado";

    private static Brush BrushFor(string severity) => severity.ToLowerInvariant() switch
    {
        "error" or "critical" => (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
        "warning" => (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
        _ => (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
    };

    private static Brush BrushForStatus(string status) => status.Contains("Atención") || status.Contains("Attention")
        ? (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
        : status.Contains("Correcto") || status.Contains("Healthy")
        ? (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
        : (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];

    private static string DeriveSystemStatus(SystemDiagnosticReport report)
    {
        if (report.Findings.Any(f => f.Severity.ToString().Equals("Error", StringComparison.OrdinalIgnoreCase))) return "Sistema: Atención";
        if (report.Findings.Any(f => f.Severity.ToString().Equals("Warning", StringComparison.OrdinalIgnoreCase))) return "Sistema: Correcto con avisos";
        return report.Findings.Count == 0 ? "Sistema: Correcto" : "Sistema: Correcto";
    }

    private async void OnAnalyzeClick(object sender, RoutedEventArgs e)
    {
        AnalyzeButton.IsEnabled = false;
        AnalyzingRing.IsActive = true;
        AnalyzeStatusText.Text = Localizer.Get("dashboard.analyzing");
        try
        {
            var candidates = AppServices.Recovery.Scan();
            AppServices.State.RecoveryCandidates = candidates.Select(candidate => candidate.OptimizationId).ToList();
            RecoveryBar.IsOpen = candidates.Count > 0;
            if (candidates.Count > 0)
            {
                RecoveryBar.Message = "Operaciones incompletas: " + string.Join(", ", candidates.Select(c => c.OptimizationId)) +
                    ". Revise Restaurar/Historial y revierta desde el servicio si procede.";
            }

            var context = await AppServices.ContextProvider.GetAsync();
            AppServices.State.Context = context;

            var network = await new CAO.Infrastructure.Networking.NetworkDiagnosticsProvider().MeasureAsync();
            var storage = new CAO.Infrastructure.Storage.StorageDiagnosticsProvider().Measure();
            var security = new CAO.Infrastructure.Security.SecurityDiagnosticsProvider().Measure();

            await Task.Run(() =>
            {
                var recommendations = RecommendationEngine.BuildAll(AppServices.Catalog, AppServices.Registry, context);
                AppServices.State.Recommendations = recommendations;

                var report = HealthEngine.Evaluate(
                    context: context,
                    network: network,
                    storage: storage,
                    security: security);
                _lastReport = report;
                DispatcherQueue.TryEnqueue(() =>
                {
                    FindingsList.ItemsSource = report.Findings
                        .Select(finding => new FindingRow(finding.Severity.ToString(), finding.MessageEs, BrushFor(finding.Severity.ToString())))
                        .ToList();
                    HealthScoresText.Text = DescribeScores(report);
                    WhyScoresButton.Visibility = string.IsNullOrWhiteSpace(HealthScoresText.Text) ? Visibility.Collapsed : Visibility.Visible;
                    EmptyFindingsState.Visibility = report.Findings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    FindingsList.Visibility = report.Findings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                });
            });

            ThermalBar.IsOpen = context.ThermalState == ThermalState.Throttling;
            RecoveryBar.IsOpen = AppServices.State.RecoveryCandidates.Count > 0;
            PendingRebootBar.IsOpen = context.PendingReboot;
            if (context.PendingReboot)
            {
                PendingRebootBar.Message = "Reinicio pendiente por: " +
                    string.Join(", ", context.PendingRebootReasons) + ".";
            }

            AppServices.State.LastAnalysisUtc = DateTime.UtcNow;
            RenderState();
        }
        catch (Exception ex)
        {
            // Fase 50: nunca exponer ex.Message crudo como UX final — usar código + SafeMessage
            SystemSummary.Text = $"{ErrorCodes.UiAnalyzeFailed}: No fue posible completar el análisis. Verifique que el servicio no esté bloqueando WMI y reintente. [Detalles técnicos: {ex.GetType().Name}]";
            AnalyzeStatusText.Text = $"{ErrorCodes.UiAnalyzeFailed}: análisis no completado";
            App.WriteCrashLog(ex);
        }
        finally
        {
            AnalyzingRing.IsActive = false;
            AnalyzeButton.IsEnabled = true;
            if (AnalyzeStatusText.Text == Localizer.Get("dashboard.analyzing")) AnalyzeStatusText.Text = "";
        }
    }

    private async void OnWhyScoresClick(object sender, RoutedEventArgs e)
    {
        if (_lastReport is null) return;
        var detail = string.Join("\n", _lastReport.Scores.Where(s => s.IsMeasured).Select(s => $"• {s.Dimension}: {s.Score}/100 — {s.ReasonEs}"));
        if (string.IsNullOrWhiteSpace(detail)) detail = "No hay dimensiones medidas suficientes para un score. Ejecute más diagnósticos.";
        var dialog = new ContentDialog
        {
            Title = "Desglose de salud del sistema",
            Content = new ScrollViewer { MaxHeight = 380, Content = new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } },
            CloseButtonText = "Cerrar",
            XamlRoot = Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private void OnGoOptimizeClick(object sender, RoutedEventArgs e)
    {
        // Navigate to Optimize — shell hosts NavigationView selection
        if (App.Current is App app && app is not null)
        {
            // Best effort: locate MainWindow via AppServices pattern
        }
    }

    private static string DescribeScores(SystemDiagnosticReport report)
    {
        var measured = report.Scores.Where(score => score.IsMeasured && score.Score is not null).ToList();
        if (measured.Count == 0) return "Sin puntuación — faltan mediciones.";
        return string.Join("  ·  ", measured.Select(score => $"{score.Dimension}: {score.Score}/100"));
    }
}
