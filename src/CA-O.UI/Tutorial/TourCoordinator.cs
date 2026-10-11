using System;
using System.Threading.Tasks;
using CAO.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CAO.UI.Tutorial;

/// <summary>
/// Drives the tour: navigates to each step's route, waits for the page to land, resolves the
/// target control by x:Name, and hands its rect to the overlay. Never throws.
/// </summary>
public sealed class TourCoordinator
{
    private readonly MainWindow _window;
    private readonly TourState _state = new();
    private string? _expectedRoute;

    public TourCoordinator(MainWindow window)
    {
        _window = window;
    }

    public static bool IsActive { get; private set; }

    public TourStep? Current => _state.Current;

    public void Start()
    {
        try
        {
            _state.Reset();
            _state.Start();
            IsActive = true;
            _window.TourOverlay.Visibility = Visibility.Visible;
            _window.TourOverlay.SetStepPosition(1, _state.Count);
            _window.TourOverlay.NextRequested -= OnNextRequested;
            _window.TourOverlay.BackRequested -= OnBackRequested;
            _window.TourOverlay.SkipRequested -= OnSkipRequested;
            _window.TourOverlay.NextRequested += OnNextRequested;
            _window.TourOverlay.BackRequested += OnBackRequested;
            _window.TourOverlay.SkipRequested += OnSkipRequested;
            ShowCurrent();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            Skip();
        }
    }

    public void Next()
    {
        try
        {
            if (_state.Next())
            {
                ShowCurrent();
                return;
            }

            Finish(dismissed: false);
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            Skip();
        }
    }

    public void Back()
    {
        try
        {
            if (_state.Back()) ShowCurrent();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            Skip();
        }
    }

    public void Skip()
    {
        var wasActive = IsActive;
        try
        {
            if (wasActive) _state.Skip();
            Finish(dismissed: wasActive);
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
        }
        finally
        {
            IsActive = false;
        }
    }

    public void OnFrameNavigated()
    {
        if (!IsActive) return;
        try
        {
            PaintCurrent();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            Skip();
        }
    }

    public void OnUserNavigation(string tag)
    {
        if (!IsActive) return;
        if (tag == _expectedRoute) return;
        try
        {
            Skip();
            _window.ShowTourInterrupted();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
        }
    }

    private void ShowCurrent()
    {
        if (!_state.IsRunning) return;
        var step = _state.Current;
        if (step is null)
        {
            Skip();
            return;
        }

        _expectedRoute = step.Route;
        _window.TourOverlay.SetStepPosition(_state.Index + 1, _state.Count);
        _window.TourOverlay.BackButtonIsEnabled = _state.CanGoBack;
        _window.TourOverlay.NextButtonText = _state.Index == _state.Count - 1 ? "Finalizar" : "Siguiente";
        _window.SelectRoute(step.Route);
        _ = PaintWhenReadyAsync(step);
    }

    private async Task PaintWhenReadyAsync(TourStep step)
    {
        var overlay = _window.TourOverlay;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var attempt = 0; attempt < overlay.MaxWaitAttempts; attempt++)
            {
                if (!_state.IsRunning) return;
                var rect = ResolveRect(step);
                if (rect is Rect found)
                {
                    if (!_state.IsRunning) return;
                    overlay.Show(step, found);
                    return;
                }

                if (sw.ElapsedMilliseconds >= overlay.WaitBudgetMilliseconds) break;
                await Task.Delay(100);
            }

            if (!_state.IsRunning) return;
            overlay.Show(step, null);
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            if (_state.IsRunning) overlay.Show(step, null);
        }
    }

    private Rect? ResolveRect(TourStep step)
    {
        if (_window.ContentFrame.Content is not FrameworkElement page) return null;
        if (page.FindName(step.TargetName) is not FrameworkElement target) return null;
        if (target.ActualWidth <= 0 || target.ActualHeight <= 0) return null;
        var origin = target.TransformToVisual(_window.TourOverlay).TransformPoint(new Point(0, 0));
        return new Rect(origin.X, origin.Y, target.ActualWidth, target.ActualHeight);
    }

    private void PaintCurrent()
    {
        if (!_state.IsRunning) return;
        var step = _state.Current;
        if (step is null) return;
        _window.TourOverlay.Show(step, ResolveRect(step));
    }

    private void Finish(bool dismissed)
    {
        IsActive = false;
        _expectedRoute = null;
        _window.TourOverlay.NextRequested -= OnNextRequested;
        _window.TourOverlay.BackRequested -= OnBackRequested;
        _window.TourOverlay.SkipRequested -= OnSkipRequested;
        _window.TourOverlay.Hide();
        if (dismissed) TourProgress.MarkDismissed();
        else if (_state.IsFinished) TourProgress.MarkCompleted();
    }

    private void OnNextRequested() => Next();
    private void OnBackRequested() => Back();
    private void OnSkipRequested() => Skip();
}
