using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CAO.UI.Helpers;

/// <summary>
/// Ancho común de página. Con HorizontalAlignment=Center cada página encogía a
/// su contenido (unas estrechas, otras anchas) y con Stretch el contenido se
/// salía por la derecha dentro del ScrollViewer. Este comportamiento fija el
/// ancho del contenedor al área visible del ScrollViewer que lo aloja, con
/// tope en MaxWidth, y lo deja centrado: todas las páginas miden lo mismo.
/// Se activa desde CaoPageContainerStyle; nunca lanza.
/// </summary>
public static class PageLayout
{
    public static readonly DependencyProperty FillViewportProperty =
        DependencyProperty.RegisterAttached(
            "FillViewport", typeof(bool), typeof(PageLayout),
            new PropertyMetadata(false, OnFillViewportChanged));

    public static bool GetFillViewport(DependencyObject obj) => (bool)obj.GetValue(FillViewportProperty);

    public static void SetFillViewport(DependencyObject obj, bool value) => obj.SetValue(FillViewportProperty, value);

    private static void OnFillViewportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true) return;
        element.Loaded += OnLoaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        try
        {
            var viewer = FindScrollViewer(element);
            if (viewer is null) return;
            void Apply()
            {
                var width = ClampWidth(viewer.ActualWidth, viewer.Padding.Left, viewer.Padding.Right, element.MaxWidth);
                if (width > 0) element.Width = width;
            }
            viewer.SizeChanged -= Handler;
            viewer.SizeChanged += Handler;
            Apply();

            void Handler(object s, SizeChangedEventArgs args) => Apply();
        }
        catch
        {
            // El ancho es cosmético: sin él la página sigue funcionando.
        }
    }

    internal static double ClampWidth(double viewerWidth, double paddingLeft, double paddingRight, double maxWidth)
    {
        var available = viewerWidth - paddingLeft - paddingRight - 16;
        if (available <= 0) return 0;
        var max = double.IsInfinity(maxWidth) || maxWidth <= 0 ? available : maxWidth;
        return Math.Min(max, available);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject element)
    {
        var current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is ScrollViewer viewer) return viewer;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
