using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace CAO.UI.Helpers;

/// <summary>
/// Micro-animaciones de la app: entrada escalonada, contadores y pulsos.
/// Todo respeta ReducedMotion: si el usuario desactiva animaciones, se aplican
/// los valores finales al instante sin Storyboards en bucle.
/// </summary>
public static class UiAnimations
{
    private static readonly TimeSpan EntranceStep = TimeSpan.FromMilliseconds(45);
    private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(380);
    private const double HoverScale = 1.02;
    private static readonly TimeSpan HoverDuration = TimeSpan.FromMilliseconds(150);

    /// <summary>Entrada en cascada para los hijos directos de un contenedor.</summary>
    public static void PlayEntrance(Panel? container)
    {
        if (container is null || !Accessibility.ReducedMotion.ShouldAnimate) return;
        try
        {
            var delay = TimeSpan.Zero;
            foreach (var child in container.Children.OfType<FrameworkElement>())
            {
                if (child.Visibility != Visibility.Visible) continue;
                var transform = new CompositeTransform { TranslateY = 26 };
                child.RenderTransform = transform;
                child.Opacity = 0;

                var fade = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = EntranceDuration,
                    BeginTime = delay,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                };
                var slide = new DoubleAnimation
                {
                    From = 26,
                    To = 0,
                    Duration = EntranceDuration,
                    BeginTime = delay,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(fade, child);
                Storyboard.SetTargetProperty(fade, "Opacity");
                Storyboard.SetTarget(slide, transform);
                Storyboard.SetTargetProperty(slide, "TranslateY");

                var board = new Storyboard();
                board.Children.Add(fade);
                board.Children.Add(slide);
                board.Begin();

                delay += EntranceStep;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"PlayEntrance failed: {ex.Message}"); }
    }

    /// <summary>Contador animado hacia el valor objetivo (no hace nada si ya coincide).</summary>
    public static void CountUp(TextBlock? target, int value, int durationMs = 550)
    {
        if (target is null) return;
        var finalText = value.ToString();
        if (!Accessibility.ReducedMotion.ShouldAnimate || target.Text == finalText)
        {
            target.Text = finalText;
            return;
        }
        try
        {
            var start = 0;
            if (int.TryParse(target.Text, out var current)) start = current;
            if (start == value) return;
            var watch = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            timer.Tick += (_, _) =>
            {
                try
                {
                    var t = Math.Min(1.0, watch.ElapsedMilliseconds / (double)durationMs);
                    var eased = 1 - Math.Pow(1 - t, 3);
                    target.Text = ((int)Math.Round(start + ((value - start) * eased))).ToString();
                    if (t >= 1) timer.Stop();
                }
                catch { timer.Stop(); }
            };
            timer.Start();
        }
        catch { target.Text = finalText; }
    }

    /// <summary>
    /// Propiedad adjunta que activa una micro-interacción de paso (escala
    /// suave + realce) sobre cualquier FrameworkElement al pasar el puntero.
    /// XAML: <code>Helpers:UiAnimations.CardHover="True"</code>.
    /// Respeta ReducedMotion y reutiliza el CompositeTransform de la entrada
    /// (no compite con PlayEntrance).
    /// </summary>
    public static readonly DependencyProperty CardHoverProperty =
        DependencyProperty.RegisterAttached(
            "CardHover", typeof(bool), typeof(UiAnimations),
            new PropertyMetadata(false, OnCardHoverChanged));

    public static bool GetCardHover(DependencyObject obj) =>
        (bool)obj.GetValue(CardHoverProperty);

    public static void SetCardHover(DependencyObject obj, bool value) =>
        obj.SetValue(CardHoverProperty, value);

    private static void OnCardHoverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el) return;
        var enable = (bool)e.NewValue;
        if (enable)
        {
            el.PointerEntered += OnCardPointerEntered;
            el.PointerExited += OnCardPointerExited;
            el.PointerCanceled += OnCardPointerExited;
        }
        else
        {
            el.PointerEntered -= OnCardPointerEntered;
            el.PointerExited -= OnCardPointerExited;
            el.PointerCanceled -= OnCardPointerExited;
        }
    }

    private static void OnCardPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement el) return;
        if (!Accessibility.ReducedMotion.ShouldAnimate) return;
        EnsureScaleTransform(el);
        AnimateScale(el, HoverScale);
    }

    private static void OnCardPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement el) return;
        EnsureScaleTransform(el);
        AnimateScale(el, 1.0);
    }

    /// <summary>
    /// Garantiza un CompositeTransform centrado reutilizable (si PlayEntrance
    /// ya puso uno, se conserva para no cortar su animación de traslación).
    /// </summary>
    private static void EnsureScaleTransform(FrameworkElement el)
    {
        if (el.RenderTransform is not CompositeTransform)
            el.RenderTransform = new CompositeTransform();
        el.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
    }

    private static void AnimateScale(FrameworkElement el, double to)
    {
        try
        {
            if (el.RenderTransform is not CompositeTransform ct) return;
            var board = new Storyboard();
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            foreach (var path in new[] { "ScaleX", "ScaleY" })
            {
                var anim = new DoubleAnimation
                {
                    To = to,
                    Duration = HoverDuration,
                    EasingFunction = ease,
                };
                Storyboard.SetTarget(anim, ct);
                Storyboard.SetTargetProperty(anim, path);
                board.Children.Add(anim);
            }
            board.Begin();
        }
        catch (Exception ex) { Debug.WriteLine($"AnimateScale failed: {ex.Message}"); }
    }
}
