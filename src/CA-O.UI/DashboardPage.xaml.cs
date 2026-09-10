using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using CAO.Core.Diagnostics;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Centro de control: héroe con CTAs, métricas en vivo (CPU/RAM/disco),
/// postura de seguridad, módulos y auditoría reciente. Todo ligado a datos
/// reales: sin análisis no hay números (se muestra "—").
/// </summary>
public sealed partial class DashboardPage : Page
{
    private readonly ViewModels.DashboardViewModel _vm;
    private bool _sampling;

    public DashboardPage()
    {
        InitializeComponent();
        try { ScoreArc.StrokeStartLineCap = PenLineCap.Round; ScoreArc.StrokeEndLineCap = PenLineCap.Round; } catch { }
        _vm = AppHost.Resolve<ViewModels.DashboardViewModel>();
        DataContext = _vm;
        ApplyTexts();
        RenderHub();
        var uiState0 = AppHost.Resolve<ViewModels.UiState>();
        uiState0.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.DashboardViewModel.Recommendations) or nameof(ViewModels.DashboardViewModel.StatusMessage))
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, RenderHub);
        };
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.UiState.Context) or nameof(ViewModels.UiState.Recommendations) or nameof(ViewModels.UiState.LastAnalysisUtc) or nameof(ViewModels.UiState.ServiceStatus) or nameof(ViewModels.UiState.UpdateAvailable) or nameof(ViewModels.UiState.LatestVersion))
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, RenderHub);
        };
        Loaded += async (_, __) =>
        {
            Helpers.UiAnimations.PlayEntrance(PageContent);
            RenderHub();
            if (uiState.Context is null)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await _vm.LoadCommand.ExecuteAsync(null);
                    RenderHub();
                }
                catch (Exception ex) { App.WriteCrashLog(ex); }
            }
            ServiceInfoBar.IsOpen = uiState.ServiceStatus is not ("connected" or "conectado");
            _ = SampleLiveAsync();
            App.BootMark("Panel interactivo");
        };
    }

    private void ApplyTexts()
    {
        AnalyzeButton.Content = Localizer.Get("dashboard.analyze");
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var when = uiState.LastAnalysisUtc?.ToLocalTime().ToString("g") ?? Localizer.Get("dashboard.never");
        LastAnalysisText.Text = $"{Localizer.Get("dashboard.lastAnalysis")}: {when}";
        AnalyzeStatusText.Text = "";
        try { Helpers.LocalizationHelper.LocalizeTree(this.Content as Microsoft.UI.Xaml.DependencyObject ?? this); } catch { }
    }

    private static bool _bootLogged;

    private void RenderHub()
    {
        ApplyTexts();
        if (!_bootLogged)
        {
            _bootLogged = true;
            App.BootMark("Panel renderizado");
        }

        var uiState = AppHost.Resolve<ViewModels.UiState>();
        var recommendations = uiState.Recommendations;
        var recCount = recommendations.Count(r => r.Bucket == RecommendationBucket.Recommended);
        var context = uiState.Context;

        OptimizeCtaButton.Content = recCount > 0 ? $"Ver {recCount} recomendadas" : "Ir a Optimizar";
        ModOptPillText.Text = recCount > 0 ? $"{recCount} Recomendadas" : "Sin pendientes";

        // Programa
        ProgramInfoText.Text = context is null
            ? $"CA-O {CAO.Shared.AppVersion.Semantic} · Protocolo IPC v{CAO.Shared.IPC.IpcProtocol.Version}"
            : $"Windows {context.WindowsEdition} build {context.WindowsBuild} · CA-O {CAO.Shared.AppVersion.Semantic} · IPC v{CAO.Shared.IPC.IpcProtocol.Version}";

        // Servicio
        var svc = uiState.ServiceStatus ?? "unknown";
        bool connected = svc is "connected" or "conectado";
        ServiceBadgeText.Text = connected
            ? $"{Localizer.Get("common.serviceStatus")}: {Localizer.Get("common.connected")}"
            : $"{Localizer.Get("common.serviceStatus")}: {Localizer.Get("common.disconnected")}";
        ServiceDot.Fill = connected
            ? (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
            : (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];

        // Salud + índice global (solo medido)
        var report = context is null ? null : HealthEngine.Evaluate(context);
        var measured = report?.Scores.Where(s => s.IsMeasured && s.Score is not null).ToList()
            ?? new List<HealthScore>();
        if (measured.Count == 0)
        {
            UpdateScoreRing(null);
            ScoreLabelText.Text = "Sin datos";
            ScoreDetailText.Text = "Ejecute Analizar para medir.";
            MeasuredNoteText.Text = string.Empty;
            FindingsLineText.Text = context is null ? string.Empty : "Sin puntuación — faltan mediciones.";
        }
        else
        {
            var avg = measured.Average(s => s.Score!.Value);
            UpdateScoreRing(avg);
            var (label, pillBg, pillFg) = avg >= 85
                ? ("Estabilidad excelente", "CaoGreenSoftBrush", "CaoGreenSolidBrush")
                : avg >= 65
                ? ("Estable", "CaoOpsCyanSoftBrush", "CaoOpsCyanBrush")
                : avg >= 45
                ? ("Atención", "CaoAmberSoftBrush", "CaoAmberSolidBrush")
                : ("Crítico", "CaoRoseSoftBrush", "CaoRoseSolidBrush");
            ScoreLabelText.Text = label;
            VerdictPillText.Text = avg >= 85 ? "ÓPTIMO" : avg >= 65 ? "ESTABLE" : avg >= 45 ? "ATENCIÓN" : "CRÍTICO";
            try
            {
                VerdictPill.Background = (Brush)Application.Current.Resources[pillBg];
                VerdictPillText.Foreground = (Brush)Application.Current.Resources[pillFg];
            }
            catch { }
            ScoreDetailText.Text = recCount > 0
                ? $"{recCount} optimizaciones recomendadas con evidencia."
                : "Sin recomendaciones pendientes.";
            MeasuredNoteText.Text = $"{measured.Count} dimensiones medidas · {string.Join(" · ", measured.Select(s => $"{Localizer.GetDimensionLabel(s.Dimension)}: {s.Score}"))}";
            // Nota: el enum es DiagnosticSeverity (Critical/Warning); "Error" nunca coincidía y los críticos no se contaban.
            var crit = report!.Findings.Count(f => f.Severity == DiagnosticSeverity.Critical);
            var warn = report.Findings.Count(f => f.Severity == DiagnosticSeverity.Warning);
            FindingsLineText.Text = crit + warn == 0 ? "Sin hallazgos" : $"{crit} críticas · {warn} avisos";
        }

        // Hardware
        CpuNameText.Text = context is null || string.IsNullOrWhiteSpace(context.CpuName) ? "CPU sin medir" : context.CpuName;
        CpuSpecText.Text = context is null ? "Ejecute Analizar." : $"{context.CpuCores} núcleos / {context.CpuLogicalProcessors} hilos · {context.Architecture}";
        GpuNameText.Text = context is null || string.IsNullOrWhiteSpace(context.GpuName) ? "GPU sin medir" : context.GpuName;
        GpuSpecText.Text = context is null ? "Ejecute Analizar."
            : string.IsNullOrWhiteSpace(context.GpuDriverVersion) ? "Driver desconocido" : $"Driver {context.GpuDriverVersion}";
        try
        {
            var hz = context is null ? 0 : Convert.ToDouble(context.DisplayRefreshHz);
            GpuExtraText.Text = hz > 0 ? $"{hz:0} Hz" : string.Empty;
        }
        catch { GpuExtraText.Text = string.Empty; }
        RamUsedText.Text = context is null ? "—" : $"{context.RamGb} GB";
        RamTotalText.Text = context is null ? string.Empty : "en total";
        RamSpecText.Text = context is null ? "Ejecute Analizar."
            : $"{(context.IsLaptop ? "Portátil" : "Sobremesa")} · {(context.HasSsd ? "SSD" : "HDD")}";
        RenderDisk();

        // Seguridad
        SetSecStatus(SecBootText, context?.SecureBootEnabled, "Habilitado", "Desactivado");
        SetSecStatus(SecVbsText, context?.VbsEnabled, "Activo", "Inactivo");
        SetSecStatus(SecHvciText, context?.HvciEnabled, "Activo", "Inactivo");
        SecSvcText.Text = connected ? "Conectado" : "No disponible";
        SecSvcText.Foreground = (Brush)Application.Current.Resources[connected ? "CaoOpsGreenBrush" : "CaoNeutralBrush"];
        var okCount = new[] { context?.SecureBootEnabled == true, context?.VbsEnabled == true, context?.HvciEnabled == true, connected }.Count(b => b);
        SecCountText.Text = $"{okCount}/4 REQUISITOS";

        // Módulo gaming
        var acCount = context?.AntiCheats.Count ?? 0;
        int gameCount = 0;
        try { gameCount = context?.GamesDetected.Count ?? 0; } catch { }
        ModGamingDescText.Text = context is null
            ? "Anti-cheat y juegos detectados."
            : acCount == 0 ? (gameCount == 0 ? "Sin anti-cheats ni juegos." : $"{gameCount} juegos · sin anti-cheats.") : $"{acCount} anti-cheats · {gameCount} juegos.";
        ModGamingPillText.Text = acCount == 0 ? "Limpio" : $"{acCount} detectados";

        // Auditoría reciente
        RenderAudit();

        // Avisos
        ThermalBar.IsOpen = context?.ThermalState == ThermalState.Throttling;
        var isSignificantReboot = context?.PendingReboot == true && context.PendingRebootReasons.Any(r => r.Contains("Windows Update", StringComparison.OrdinalIgnoreCase) || r.Contains("Component Based Servicing", StringComparison.OrdinalIgnoreCase));
        PendingRebootBar.IsOpen = isSignificantReboot;
        if (isSignificantReboot)
            PendingRebootBar.Message = "Reinicio pendiente por: " + string.Join(", ", context!.PendingRebootReasons) + ".";
        RecoveryBar.IsOpen = uiState.RecoveryCandidates.Count > 0;
        ServiceInfoBar.IsOpen = !connected;
        UpdateBar.IsOpen = uiState.UpdateAvailable && !string.IsNullOrWhiteSpace(uiState.LatestVersion);
        if (UpdateBar.IsOpen)
            UpdateBar.Message = $"Hay una versión nueva de CA-O ({uiState.LatestVersion}).";
        foreach (var bar in new[] { RecoveryBar, ThermalBar, PendingRebootBar, ServiceInfoBar, UpdateBar })
            bar.Visibility = bar.IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void SetSecStatus(TextBlock target, bool? enabled, string on, string off)
    {
        target.Text = enabled is null ? "Desconocido" : enabled.Value ? on : off;
        try
        {
            target.Foreground = (Brush)Application.Current.Resources[enabled is null ? "CaoNeutralBrush" : enabled.Value ? "CaoOpsGreenBrush" : "CaoOpsRedBrush"];
        }
        catch { }
    }

    private void RenderDisk()
    {
        try
        {
            var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady &&
                string.Equals(NormalizeRoot(d.Name), NormalizeRoot(systemRoot), StringComparison.OrdinalIgnoreCase));
            if (drive is null || drive.TotalSize <= 0)
            {
                DiskFreeText.Text = "—"; DiskTotalText.Text = string.Empty;
                DiskSpecText.Text = "Disco no legible."; DiskUsePctText.Text = "—"; DiskUseBar.Value = 0;
                return;
            }
            var freeGb = drive.TotalFreeSpace / 1024d / 1024 / 1024;
            var totalGb = drive.TotalSize / 1024d / 1024 / 1024;
            var usedPct = (1 - (double)drive.TotalFreeSpace / drive.TotalSize) * 100;
            DiskFreeText.Text = $"{freeGb:0} GB";
            DiskTotalText.Text = $"libres en {drive.Name.TrimEnd('\\')} ({totalGb:0} GB)";
            DiskSpecText.Text = $"{drive.DriveFormat} · {drive.VolumeLabel}";
            DiskUseBar.Value = usedPct;
            DiskUsePctText.Text = $"{usedPct:0}%";
            DiskTitleText.Text = $"Disco {drive.Name.TrimEnd('\\')}";
        }
        catch
        {
            DiskFreeText.Text = "—"; DiskUsePctText.Text = "—"; DiskUseBar.Value = 0;
        }
    }

    private static string NormalizeRoot(string root) => root.TrimEnd('\\', '/').ToUpperInvariant();

    private void UpdateScoreRing(double? avg)
    {
        if (avg is null || avg <= 0)
        {
            ScoreArc.Visibility = Visibility.Collapsed;
            ScoreValueText.Text = "—";
            return;
        }
        ScoreArc.Visibility = Visibility.Visible;
        var v = Math.Clamp(avg.Value, 0, 100);
        Helpers.UiAnimations.CountUp(ScoreValueText, (int)Math.Round(v));
        var angle = Math.Min(v / 100 * 360, 359.99);
        var rad = (angle - 90) * Math.PI / 180;
        ScoreSegment.Point = new Point(60 + 54 * Math.Cos(rad), 60 + 54 * Math.Sin(rad));
        ScoreSegment.IsLargeArc = angle > 180;
    }

    private void RenderAudit()
    {
        AuditRows.Children.Clear();
        try
        {
            var history = AppHost.Resolve<CAO.Infrastructure.Logging.JsonHistoryLogger>();
            var entries = history.ReadLast(3).Reverse().ToList();
            try
            {
                var warnings = history.VerifyIntegrity();
                HashChainText.Text = warnings.Count == 0 ? "● HASH-CHAIN ÍNTEGRO" : $"● {warnings.Count} ILEGIBLES";
            }
            catch { HashChainText.Text = string.Empty; }
            AuditEmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var entry in entries)
            {
                AuditRows.Children.Add(BuildAuditRow(entry));
            }
        }
        catch (Exception ex)
        {
            AuditEmptyText.Visibility = Visibility.Visible;
            AuditEmptyText.Text = "Historial no disponible en este momento.";
            try { App.WriteCrashLog(ex); } catch { }
        }
    }

    private Grid BuildAuditRow(dynamic entry)
    {
        bool ok;
        string id, op, detail, when;
        try { ok = (bool)entry.Success; } catch { ok = false; }
        try { id = (string?)entry.OptimizationId ?? "—"; } catch { id = "—"; }
        try { op = (string?)entry.Operation ?? string.Empty; } catch { op = string.Empty; }
        try
        {
            var tx = (string?)entry.SnapshotId;
            var err = (string?)entry.Error;
            detail = !string.IsNullOrWhiteSpace(err) ? err : $"TX: {(string.IsNullOrWhiteSpace(tx) ? "—" : tx)}";
        }
        catch { detail = string.Empty; }
        try { when = RelativeEs(((DateTime)entry.TimestampUtc).ToLocalTime()); } catch { when = string.Empty; }

        var pill = op.Equals("revert", StringComparison.OrdinalIgnoreCase) ? "REVERTIDO"
            : op.Equals("verify", StringComparison.OrdinalIgnoreCase) ? (ok ? "VERIFICADO" : "FALLO")
            : ok ? "APLICADO" : "FALLO";

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBg = (Brush)Application.Current.Resources[ok ? "CaoGreenSoftBrush" : "CaoRoseSoftBrush"];
        var iconFg = (Brush)Application.Current.Resources[ok ? "CaoGreenSolidBrush" : "CaoRoseSolidBrush"];
        var icon = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(16),
            Background = iconBg, VerticalAlignment = VerticalAlignment.Center,
            Child = new FontIcon
            {
                Glyph = ok ? "\uE930" : "\uE783",
                FontSize = 14, Foreground = iconFg,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(new TextBlock
        {
            Text = id, FontFamily = new FontFamily("Consolas"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13,
        });
        var pillBorder = new Border
        {
            CornerRadius = new CornerRadius(5), Padding = new Thickness(7, 2, 7, 2),
            Background = iconBg, VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = pill, FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = iconFg },
        };
        try { ((TextBlock)pillBorder.Child).CharacterSpacing = 60; } catch { }
        head.Children.Add(pillBorder);
        texts.Children.Add(head);
        texts.Children.Add(new TextBlock { Text = detail, FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        var right = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var time = new TextBlock { Text = when, FontSize = 11, Opacity = 0.8, HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(time);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        var wrap = new Grid();
        wrap.Children.Add(grid);
        return wrap;
    }

    private static string RelativeEs(DateTime local)
    {
        var d = DateTime.Now - local;
        if (d.TotalMinutes < 1) return "Ahora mismo";
        if (d.TotalMinutes < 60) return $"Hace {(int)d.TotalMinutes} min";
        if (d.TotalHours < 24) return $"Hace {(int)d.TotalHours} h";
        if (d.TotalDays < 2) return $"Ayer {local:HH:mm}";
        return $"Hace {(int)d.TotalDays} días";
    }

    private async Task SampleLiveAsync()
    {
        if (_sampling) return;
        _sampling = true;
        try
        {
            // Todo lo bloqueante (init de contadores + espera) fuera del hilo UI.
            var sample = await Task.Run(async () =>
            {
                float load = -1, avail = -1;
                try
                {
                    using var cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                    using var mem = new PerformanceCounter("Memory", "Available MBytes", true);
                    try { cpu.NextValue(); } catch { }
                    try { avail = mem.NextValue(); } catch { }
                    await Task.Delay(900);
                    try { load = cpu.NextValue(); } catch { }
                }
                catch { }
                return (load, avail);
            });
            var uiState = AppHost.Resolve<ViewModels.UiState>();
            var totalMb = (uiState.Context?.RamGb ?? 0) * 1024d;
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (sample.load > 0.5)
                    {
                        CpuLoadBar.Value = Math.Clamp(sample.load, 0, 100);
                        CpuLoadText.Text = $"{sample.load:0}%";
                    }
                    else { CpuLoadText.Text = "—"; }
                    if (totalMb > 0 && sample.avail > 0)
                    {
                        var usedPct = Math.Clamp((totalMb - sample.avail) / totalMb * 100, 0, 100);
                        RamUseBar.Value = usedPct;
                        RamUsePctText.Text = $"{usedPct:0}%";
                        RamUsedText.Text = $"{(totalMb - sample.avail) / 1024:0.0} GB";
                    }
                }
                catch { }
            });
        }
        catch (Exception ex) { try { App.WriteCrashLog(ex); } catch { } }
    }

    private async void OnAnalyzeClick(object sender, RoutedEventArgs e)
    {
        AnalyzeButton.IsEnabled = false;
        AnalyzingRing.IsActive = true;
        AnalyzeStatusText.Text = Localizer.Get("dashboard.analyzing");
        try
        {
            if (MainWindow.Current is not null)
                await MainWindow.Current.GoAnalyzeAndRunAsync();
            else
                AppHost.Resolve<Navigation.INavigationService>().Select("analyze");
        }
        catch (Exception ex)
        {
            AnalyzeStatusText.Text = "No se pudo iniciar el análisis.";
            App.WriteCrashLog(ex);
        }
        finally
        {
            AnalyzingRing.IsActive = false;
            AnalyzeButton.IsEnabled = true;
            if (AnalyzeStatusText.Text == Localizer.Get("dashboard.analyzing")) AnalyzeStatusText.Text = "";
        }
    }

    private void OnTileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        if (MainWindow.Current != null) MainWindow.Current.SelectRoute(tag);
        else
        {
            var nav = AppHost.Resolve<Navigation.INavigationService>();
            nav.Select(tag);
        }
    }

    private void OnGoOptimizeClick(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Current != null) MainWindow.Current.SelectRoute("optimize");
        else
        {
            var nav = AppHost.Resolve<Navigation.INavigationService>();
            nav.Select("optimize");
        }
    }

    private void OnGoUpdateClick(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Current != null) MainWindow.Current.SelectRoute("settings");
        else
        {
            var nav = AppHost.Resolve<Navigation.INavigationService>();
            nav.Select("settings");
        }
    }
}
