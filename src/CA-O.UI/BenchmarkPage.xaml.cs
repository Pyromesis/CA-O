using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.Benchmarking;
using CAO.Shared;

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
        BaselineButton.Content = Localizer.Get("benchmark.baseline");
        AfterButton.Content = Localizer.Get("benchmark.after");
        _vm = AppHost.Resolve<ViewModels.BenchmarkViewModel>();
        DataContext = _vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(ViewModels.BenchmarkViewModel.BaselineSummary) or nameof(ViewModels.BenchmarkViewModel.ComparisonSummary) or nameof(ViewModels.BenchmarkViewModel.CurrentStep) or nameof(ViewModels.BenchmarkViewModel.Verdict))
                DispatcherQueue.TryEnqueue(RenderVm);
        };
    }

    private void RenderVm()
    {
        BaselineText.Text = _vm.BaselineSummary;
        ComparisonText.Text = _vm.ComparisonSummary;
        if (BenchStatusText is not null) BenchStatusText.Text = _vm.Status;
        if (CurrentStepText is not null) CurrentStepText.Text = _vm.CurrentStep;
        if (VerdictText is not null) VerdictText.Text = _vm.Verdict;
        if (StepCard is not null) StepCard.Visibility = string.IsNullOrWhiteSpace(_vm.CurrentStep) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnBaselineClick(object sender, RoutedEventArgs e) => await RunWithVm(true, BaselineButton);
    private async void OnAfterClick(object sender, RoutedEventArgs e) => await RunWithVm(false, AfterButton);

    private async Task RunWithVm(bool isBaseline, Button button)
    {
        var previous = button.Content;
        button.IsEnabled = false;
        Ring.IsActive = true;
        if (BenchStatusText is not null) BenchStatusText.Text = "Midiendo…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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
            if (BenchStatusText is not null && BenchStatusText.Text == "Midiendo…") BenchStatusText.Text = _vm.Status;
        }
    }
}
