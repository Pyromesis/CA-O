using System;
using System.Collections.Generic;
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
    private bool _isPageTour;

    public TourCoordinator(MainWindow window)
    {
        _window = window;
    }

    public static bool IsActive { get; private set; }

    public TourStep? Current => _state.Current;

    /// <summary>
    /// Pure navigation decision, unit-testable: only navigate when the step's route
    /// differs from the page already on screen. Unknown current pages always navigate.
    /// </summary>
    public static bool NeedsNavigation(string? currentRouteTag, string stepRoute) =>
        !string.Equals(currentRouteTag, stepRoute, StringComparison.Ordinal);

    /// <summary>Reverse lookup of <c>RouteTable.Routes</c>: page type to route tag.</summary>
    public static string? CurrentRouteTagFor(Type? pageType)
    {
        if (pageType is null) return null;
        foreach (var entry in CAO.UI.Navigation.RouteTable.Routes)
        {
            if (entry.Value == pageType) return entry.Key;
        }

        return null;
    }

    public void Start()
    {
        try
        {
            _state.Reset();
            _state.Start();
            _isPageTour = false;
            IsActive = true;
            AttachOverlay();
            ShowCurrent();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            Skip();
        }
    }

    /// <summary>
    /// Starts a page tour over an arbitrary step list with the same overlay, back, next
    /// and skip behavior. Never launches while another tour is active, never starts over
    /// an empty list, and never touches the general tour's <c>TourProgress</c> flags.
    /// </summary>
    public void StartWith(IReadOnlyList<TourStep> steps)
    {
        try
        {
            if (IsActive) return;
            if (steps is null || steps.Count == 0) return;
            _state.Reset();
            _state.StartWith(steps);
            if (!_state.IsRunning) return;
            _isPageTour = true;
            IsActive = true;
            AttachOverlay();
            ShowCurrent();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            Skip();
        }
    }

    private void AttachOverlay()
    {
        _window.TourOverlay.Visibility = Visibility.Visible;
        _window.TourOverlay.SetStepPosition(1, _state.Count);
        _window.TourOverlay.NextRequested -= OnNextRequested;
        _window.TourOverlay.BackRequested -= OnBackRequested;
        _window.TourOverlay.SkipRequested -= OnSkipRequested;
        _window.TourOverlay.NextRequested += OnNextRequested;
        _window.TourOverlay.BackRequested += OnBackRequested;
        _window.TourOverlay.SkipRequested += OnSkipRequested;
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
        var currentTag = CurrentRouteTagFor(_window.ContentFrame.Content?.GetType());
        if (NeedsNavigation(currentTag, step.Route))
        {
            _window.SelectRoute(step.Route);
        }
        // Else already on the step's page (the common page-tour case): painting is enough,
        // no navigation churn, no re-entrant Navigated events.

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
                if (!_state.IsRunning || !ReferenceEquals(_state.Current, step)) return;
                if (ResolveTarget(step) is FrameworkElement target)
                {
                    // CAO-BUG tour: los objetivos bajo el pliegue (tarjetas de abajo de
                    // Limpieza, Red gaming...) quedaban fuera de pantalla: todo oscuro y
                    // nada resaltado. Primero se trae el control a la vista y se espera a
                    // que el scroll y las animaciones de entrada asienten.
                    target.StartBringIntoView(new BringIntoViewOptions
                    {
                        AnimationDesired = false,
                        VerticalAlignmentRatio = 0.3,
                    });
                    await Task.Delay(260);
                    if (!_state.IsRunning || !ReferenceEquals(_state.Current, step)) return;
                    overlay.Show(step, RectOf(target));
                    StartTracking(step);
                    return;
                }

                if (sw.ElapsedMilliseconds >= overlay.WaitBudgetMilliseconds) break;
                await Task.Delay(100);
            }

            if (!_state.IsRunning || !ReferenceEquals(_state.Current, step)) return;
            overlay.Show(step, null);
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            if (_state.IsRunning) overlay.Show(step, null);
        }
    }

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _tracker;

    /// <summary>
    /// Mientras el paso está en pantalla el foco sigue al control: el usuario puede
    /// hacer scroll, la ventana cambiar de tamaño o la página terminar su animación
    /// de entrada (antes el foco se calculaba a mitad de la animación y quedaba
    /// desplazado respecto al botón).
    /// </summary>
    private void StartTracking(TourStep step)
    {
        StopTracking();
        try
        {
            _tracker = _window.DispatcherQueue.CreateTimer();
            _tracker.Interval = TimeSpan.FromMilliseconds(200);
            _tracker.Tick += (_, _) =>
            {
                try
                {
                    if (!_state.IsRunning || !ReferenceEquals(_state.Current, step))
                    {
                        StopTracking();
                        return;
                    }
                    _window.TourOverlay.Reposition(ResolveRect(step));
                }
                catch (Exception ex)
                {
                    App.WriteCrashLog(ex);
                    StopTracking();
                }
            };
            _tracker.Start();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
        }
    }

    private void StopTracking()
    {
        try { _tracker?.Stop(); } catch { }
        _tracker = null;
    }

    private FrameworkElement? ResolveTarget(TourStep step)
    {
        if (_window.ContentFrame.Content is not FrameworkElement page) return null;
        if (page.FindName(step.TargetName) is not FrameworkElement target) return null;
        if (HasArea(target)) return target;
        // Textos de resultado vacíos (aún no se ha ejecutado nada) o controles
        // ocultos miden 0: antes el paso salía sin foco. Se resalta la tarjeta
        // que los contiene, que es lo que el texto del paso describe.
        DependencyObject? current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(target);
        while (current is not null && !ReferenceEquals(current, page))
        {
            if (current is Border border && HasArea(border)) return border;
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static bool HasArea(FrameworkElement element) =>
        element.Visibility == Visibility.Visible && element.ActualWidth > 0 && element.ActualHeight > 0;

    private Rect? RectOf(FrameworkElement target)
    {
        var origin = target.TransformToVisual(_window.TourOverlay).TransformPoint(new Point(0, 0));
        return new Rect(origin.X, origin.Y, target.ActualWidth, target.ActualHeight);
    }

    private Rect? ResolveRect(TourStep step) =>
        ResolveTarget(step) is FrameworkElement target ? RectOf(target) : null;

    private void PaintCurrent()
    {
        if (!_state.IsRunning) return;
        var step = _state.Current;
        if (step is null) return;
        // Tras navegar la página aún no tiene layout: pintar sin objetivo hacía
        // parpadear la tarjeta centrada. PaintWhenReadyAsync ya está esperando el control.
        if (ResolveRect(step) is Rect rect) _window.TourOverlay.Show(step, rect);
    }

    private void Finish(bool dismissed)
    {
        IsActive = false;
        StopTracking();
        _expectedRoute = null;
        var wasPageTour = _isPageTour;
        _isPageTour = false;
        _window.TourOverlay.NextRequested -= OnNextRequested;
        _window.TourOverlay.BackRequested -= OnBackRequested;
        _window.TourOverlay.SkipRequested -= OnSkipRequested;
        _window.TourOverlay.Hide();
        if (wasPageTour) return;
        if (dismissed) TourProgress.MarkDismissed();
        else if (_state.IsFinished) TourProgress.MarkCompleted();
    }

    private void OnNextRequested() => Next();
    private void OnBackRequested() => Back();
    private void OnSkipRequested() => Skip();
}
