using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;
using CAO.UI.Helpers;

namespace CAO.UI.Pages;

/// <summary>
/// Benchmark page (spec 66-70, 107-108): baseline vs after with an explicit
/// noise floor. If the delta is insignificant the verdict says so and the
/// change should not be kept just because "it is an optimization".
/// </summary>
public sealed partial class BenchmarkPage : Page
{
    private readonly ViewModels.BenchmarkViewModel _vm;

    public BenchmarkPage()
    {
        InitializeComponent();
        _vm = AppHost.Resolve<ViewModels.BenchmarkViewModel>();
        DataContext = _vm;
        ApplyTexts();
        var uiState = AppHost.Resolve<ViewModels.UiState>();
        uiState.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.BenchmarkViewModel.BaselineSummary) or nameof(ViewModels.BenchmarkViewModel.ComparisonSummary) or nameof(ViewModels.BenchmarkViewModel.CurrentStep) or nameof(ViewModels.BenchmarkViewModel.Verdict) or nameof(ViewModels.BenchmarkViewModel.ContextNote))
                DispatcherQueue.TryEnqueue(RenderVm);
        };
    }

    private void ApplyTexts()
    {
        BaselineButton.Content = Localizer.Get("benchmark.baseline");
        AfterButton.Content = Localizer.Get("benchmark.after");
        try { LocalizationHelper.LocalizeTree(this.Content as DependencyObject ?? this); } catch { }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyTexts();
    }

    private void RenderVm()
    {
        BaselineText.Text = _vm.BaselineSummary;
        ComparisonText.Text = _vm.ComparisonSummary;
        if (BenchStatusText is not null) BenchStatusText.Text = _vm.Status;
        if (CurrentStepText is not null) CurrentStepText.Text = _vm.CurrentStep;
        if (VerdictText is not null) VerdictText.Text = _vm.Verdict;
        if (ContextText is not null && !string.IsNullOrWhiteSpace(_vm.ContextNote)) ContextText.Text = _vm.ContextNote;
        if (StepCard is not null) StepCard.Visibility = string.IsNullOrWhiteSpace(_vm.CurrentStep) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnGoOptimizeClick(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Current is not null) MainWindow.Current.SelectRoute("optimize");
        else AppHost.Resolve<Navigation.INavigationService>().Select("optimize");
    }

    private void OnGoCleanupClick(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Current is not null) MainWindow.Current.SelectRoute("cleanup");
        else AppHost.Resolve<Navigation.INavigationService>().Select("cleanup");
    }

    private async void OnBaselineClick(object sender, RoutedEventArgs e) => await RunWithVm(true, BaselineButton);
    private async void OnAfterClick(object sender, RoutedEventArgs e) => await RunWithVm(false, AfterButton);

    private async Task RunWithVm(bool isBaseline, Button button)
    {
        var previous = button.Content;
        button.IsEnabled = false;
        Ring.IsActive = true;
        if (BenchStatusText is not null) BenchStatusText.Text = "Midiendo 3 trials con warmup…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            await _vm.RunAsync(isBaseline, cts.Token);
            RenderVm();
        }
        catch (Exception ex)
        {
            ComparisonText.Text = $"{ErrorCodes.UiBenchmarkFailed}: El benchmark no pudo completarse. [Técnico: {ex.GetType().Name}]";
            App.WriteCrashLog(ex);
        }
        finally
        {
            Ring.IsActive = false;
            button.Content = previous;
            button.IsEnabled = true;
            if (BenchStatusText is not null && BenchStatusText.Text == "Midiendo 3 trials con warmup…") BenchStatusText.Text = _vm.Status;
        }
    }
}
