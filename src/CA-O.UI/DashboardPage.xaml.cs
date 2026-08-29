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
    private readonly ViewModels.DashboardViewModel _vm;

    public DashboardPage()
    {
        InitializeComponent();
        _vm = AppHost.Resolve<ViewModels.DashboardViewModel>();
        DataContext = _vm;
        ApplyTexts();
        RenderState();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.DashboardViewModel.Health) or nameof(ViewModels.DashboardViewModel.Recommendations) or nameof(ViewModels.DashboardViewModel.StatusMessage))
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, RenderState);
        };
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.UiState.Context) or nameof(ViewModels.UiState.Recommendations) or nameof(ViewModels.UiState.LastAnalysisUtc) or nameof(ViewModels.UiState.ServiceStatus))
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, RenderState);
        };
        Loaded += async (_, __) =>
        {
            // Perceived startup <500ms: UI primero, diagnóstico pesado después (§87-88)
            RenderState();
            if (uiState.Context is null)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await _vm.LoadCommand.ExecuteAsync(null);
                    RenderState();
                }
                catch (Exception ex) { App.WriteCrashLog(ex); }
            }
            ServiceInfoBar.IsOpen = uiState.ServiceStatus is "unavailable" or "unknown";
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
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var when = uiState.LastAnalysisUtc?.ToLocalTime().ToString("g") ?? Localizer.Get("dashboard.never");
        LastAnalysisText.Text = $"{Localizer.Get("dashboard.lastAnalysis")}: {when}";
        AnalyzeStatusText.Text = "";
    }

    private void RenderState()
    {
        ApplyTexts();

        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var recommendations = uiState.Recommendations;
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

        var context = uiState.Context;
        ThermalBar.IsOpen = context?.ThermalState == ThermalState.Throttling;
        // Solo mostrar reinicio pendiente si es por Windows Update o CBS; file rename aislado (Edge/Chrome/fonts) no bloquea
        var isSignificantReboot = context?.PendingReboot == true && context.PendingRebootReasons.Any(r => r.Contains("Windows Update", StringComparison.OrdinalIgnoreCase) || r.Contains("Component Based Servicing", StringComparison.OrdinalIgnoreCase));
        PendingRebootBar.IsOpen = isSignificantReboot;
        if (isSignificantReboot)
            PendingRebootBar.Message = "Reinicio pendiente por: " + string.Join(", ", context!.PendingRebootReasons) + ".";
        else if (context?.PendingReboot == true)
            PendingRebootBar.IsOpen = false; // file rename solo -> silenciar banner, sigue en contexto para gating
        RecoveryBar.IsOpen = uiState.RecoveryCandidates.Count > 0;
        ServiceInfoBar.IsOpen = uiState.ServiceStatus is "unavailable" or "unknown";

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

        // Health scores (Fase 6: never magic number alone) — via ViewModel
        if (_vm.Health is not null)
        {
            HealthScoresText.Text = DescribeScores(_vm.Health);
            WhyScoresButton.Visibility = string.IsNullOrWhiteSpace(HealthScoresText.Text) ? Visibility.Collapsed : Visibility.Visible;
            var findings = _vm.Health.Findings.Select(f => new FindingRow(f.Severity.ToString(), f.MessageEs, BrushFor(f.Severity.ToString()))).ToList();
            FindingsList.ItemsSource = findings;
            FindingsList.Visibility = findings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            EmptyFindingsState.Visibility = findings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SystemHealthText.Text = DeriveSystemStatus(_vm.Health);
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
        // Respect reduced motion: no pulse animation if disabled
        var animate = CAO.UI.Accessibility.ReducedMotion.ShouldAnimate;
        if (!animate) AnalyzingRing.IsActive = false;
        AnalyzeStatusText.Text = Localizer.Get("dashboard.analyzing");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _vm.AnalyzeCommand.ExecuteAsync(null);
            RenderState();
            if (!string.IsNullOrEmpty(_vm.StatusMessage) && _vm.StatusMessage.Contains(ErrorCodes.UiAnalyzeFailed))
            {
                SystemSummary.Text = $"{_vm.StatusMessage} [Detalles técnicos: {_vm.StatusMessage}]";
                AnalyzeStatusText.Text = _vm.StatusMessage;
            }
            else
            {
                var uiState = AppHost.Resolve<ViewModels.UiState>();
                var context = uiState.Context;
                ThermalBar.IsOpen = context?.ThermalState == ThermalState.Throttling;
                RecoveryBar.IsOpen = uiState.RecoveryCandidates.Count > 0;
                var isSignificant2 = context?.PendingReboot == true && context.PendingRebootReasons.Any(r => r.Contains("Windows Update", StringComparison.OrdinalIgnoreCase) || r.Contains("Component Based Servicing", StringComparison.OrdinalIgnoreCase));
                PendingRebootBar.IsOpen = isSignificant2;
                if (isSignificant2)
                    PendingRebootBar.Message = "Reinicio pendiente por: " + string.Join(", ", context!.PendingRebootReasons) + ".";
                RenderState();
            }
        }
        catch (Exception ex)
        {
            SystemSummary.Text = $"{ErrorCodes.UiAnalyzeFailed}: No fue posible completar el análisis. Verifique que el servicio no esté bloqueando WMI y reintente. [Detalles técnicos: {ex.GetType().Name}]";
            AnalyzeStatusText.Text = $"{ErrorCodes.UiAnalyzeFailed}: análisis no completado";
            App.WriteCrashLog(ex);
        }
        finally
        {
            AnalyzingRing.IsActive = false;
            AnalyzeButton.IsEnabled = true;
            if (AnalyzeStatusText.Text == Localizer.Get("dashboard.analyzing")) AnalyzeStatusText.Text = _vm.StatusMessage;
        }
    }

    private async void OnWhyScoresClick(object sender, RoutedEventArgs e)
    {
        if (_vm.Health is null) return;
        var detail = string.Join("\n", _vm.Health.Scores.Where(s => s.IsMeasured).Select(s => $"• {s.Dimension}: {s.Score}/100 — {s.ReasonEs}"));
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
