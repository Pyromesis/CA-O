using System;
using CAO.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CAO.UI.Tutorial;

/// <summary>
/// Dim + luminous hole + explanatory card. Mounted as a sibling of ContentFrame inside the
/// NavigationView, so the sidebar and the title bar stay clickable while the tour runs.
/// </summary>
public sealed partial class TourOverlay : UserControl
{
    public TourOverlay()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;
    }

    public event Action? NextRequested;
    public event Action? BackRequested;
    public event Action? SkipRequested;

    public Rect? LastTargetRect { get; private set; }

    public double WaitBudgetMilliseconds => 2000;
    public int MaxWaitAttempts => 20;

    public void Show(TourStep step, Rect? targetRect)
    {
        LastTargetRect = targetRect;
        TitleText.Text = step.Title;
        BodyText.Text = step.Body;
        Visibility = Visibility.Visible;

        if (targetRect is Rect rect && rect.Width > 0 && rect.Height > 0)
        {
            ApplySpotlight(rect);
        }
        else
        {
            // Degraded mode: no target found, the card is shown centred with no dim.
            ClearSpotlight();
        }

        ApplyMood(step.Mood);
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        LastTargetRect = null;
        ClearSpotlight();
        Mascot.Set("Idle");
    }

    private void ApplySpotlight(Rect rect)
    {
        var w = Root.ActualWidth;
        var h = Root.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var clamped = Clamp(rect, w, h);

        Spotlight.Width = clamped.Width;
        Spotlight.Height = clamped.Height;
        Canvas.SetLeft(Spotlight, clamped.X);
        Canvas.SetTop(Spotlight, clamped.Y);

        DimTop.Width = w;
        DimTop.Height = Math.Max(0, clamped.Y);
        Canvas.SetLeft(DimTop, 0);
        Canvas.SetTop(DimTop, 0);

        DimLeft.Width = Math.Max(0, clamped.X);
        DimLeft.Height = clamped.Height;
        Canvas.SetLeft(DimLeft, 0);
        Canvas.SetTop(DimLeft, clamped.Y);

        DimRight.Width = Math.Max(0, w - clamped.X - clamped.Width);
        DimRight.Height = clamped.Height;
        Canvas.SetLeft(DimRight, clamped.X + clamped.Width);
        Canvas.SetTop(DimRight, clamped.Y);

        DimBottom.Width = w;
        DimBottom.Height = Math.Max(0, h - clamped.Y - clamped.Height);
        Canvas.SetLeft(DimBottom, 0);
        Canvas.SetTop(DimBottom, clamped.Y + clamped.Height);
    }

    private static Rect Clamp(Rect rect, double w, double h)
    {
        var x = Math.Clamp(rect.X, 0, w);
        var y = Math.Clamp(rect.Y, 0, h);
        return new Rect(
            x,
            y,
            Math.Clamp(rect.Width, 0, w - x),
            Math.Clamp(rect.Height, 0, h - y));
    }

    private void ClearSpotlight()
    {
        foreach (var panel in new[] { DimTop, DimBottom, DimLeft, DimRight })
        {
            panel.Width = 0;
            panel.Height = 0;
        }

        Spotlight.Width = 0;
        Spotlight.Height = 0;
    }

    private void ApplyMood(string mood)
    {
        if (mood == "Celebrate")
        {
            Mascot.CelebrateThenIdle(DispatcherQueue);
            return;
        }

        Mascot.Set(mood);
    }

    public void SetStepPosition(int index, int total)
    {
        StepIndexText.Text = $"Paso {index} de {total}";
    }

    // These two are plain CLR properties, not DependencyProperties: the setters write straight
    // to the named buttons so no INotifyPropertyChanged plumbing is needed. Task 6 drives both.
    public string NextButtonText
    {
        get => NextButton.Content as string ?? "Siguiente";
        set
        {
            if (NextButton is not null) NextButton.Content = value;
        }
    }

    public bool BackButtonIsEnabled
    {
        get => BackButton?.IsEnabled ?? false;
        set
        {
            if (BackButton is not null) BackButton.IsEnabled = value;
        }
    }

    private void OnNextClick(object sender, RoutedEventArgs e) => NextRequested?.Invoke();
    private void OnBackClick(object sender, RoutedEventArgs e) => BackRequested?.Invoke();
    private void OnSkipClick(object sender, RoutedEventArgs e) => SkipRequested?.Invoke();
}
