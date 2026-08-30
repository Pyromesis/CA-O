using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Input;
using CAO.Infrastructure.SystemInterop;
using CAO.Shared;
using CAO.UI.Helpers;

namespace CAO.UI.Pages;

/// <summary>
/// Hardware diagnostics (spec 18, 30-35): input, thermal and live
/// performance snapshots. Read-only; no keystroke or pointer content is ever
/// captured (spec 116).
/// </summary>
public sealed partial class DiagnosticsPage : Page
{
    private readonly ViewModels.DiagnosticsViewModel _vm;

    public DiagnosticsPage()
    {
        InitializeComponent();
        _vm = AppHost.Resolve<ViewModels.DiagnosticsViewModel>();
        DataContext = _vm;
        ApplyTexts();
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.DiagnosticsViewModel.InputSummary) or nameof(ViewModels.DiagnosticsViewModel.ThermalSummary) or nameof(ViewModels.DiagnosticsViewModel.PerformanceSummary))
                DispatcherQueue.TryEnqueue(RenderVm);
        };
    }

    private void ApplyTexts()
    {
        RunButton.Content = Localizer.Get("diagnostics.runAll");
        try { LocalizationHelper.LocalizeTree(this.Content as DependencyObject ?? this); } catch { }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyTexts();
    }

    private void RenderVm()
    {
        InputText.Text = _vm.InputSummary;
        ThermalText.Text = _vm.ThermalSummary;
        PerfText.Text = _vm.PerformanceSummary;
        if (StatusText is not null) StatusText.Text = _vm.Status;
    }

    private async void OnRunClick(object sender, RoutedEventArgs e)
    {
        RunButton.IsEnabled = false;
        Ring.IsActive = true;
        if (StatusText is not null) StatusText.Text = "Midiendo…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await _vm.RunCommand.ExecuteAsync(null);
            RenderVm();
        }
        catch (Exception ex)
        {
            InputText.Text = $"{ErrorCodes.UiDiagnosticsFailed}: No fue posible completar el diagnóstico. [Técnico: {ex.GetType().Name}]";
            if (StatusText is not null) StatusText.Text = $"{ErrorCodes.UiDiagnosticsFailed}: diagnóstico no completado";
            App.WriteCrashLog(ex);
        }
        finally
        {
            Ring.IsActive = false;
            RunButton.IsEnabled = true;
            if (StatusText is not null && StatusText.Text == "Midiendo…") StatusText.Text = _vm.Status;
        }
    }
}
