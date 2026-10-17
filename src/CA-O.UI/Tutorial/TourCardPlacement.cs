using System;
using Windows.Foundation;

namespace CAO.UI.Tutorial;

/// <summary>
/// Where the tour card goes relative to the highlighted control. Pure geometry,
/// unit-testable: below the target if it fits, else above, else beside it, and
/// always fully inside the host. Without a target the card is centred.
/// Before this the card was pinned bottom-left and covered whatever sat there.
/// </summary>
public static class TourCardPlacement
{
    public const double Gap = 16;
    public const double Edge = 16;

    public static Point Place(Rect? target, Size card, Size host)
    {
        var maxX = Math.Max(Edge, host.Width - card.Width - Edge);
        var maxY = Math.Max(Edge, host.Height - card.Height - Edge);

        if (target is not Rect t || t.Width <= 0 || t.Height <= 0)
        {
            return new Point(
                Math.Clamp((host.Width - card.Width) / 2, Edge, maxX),
                Math.Clamp((host.Height - card.Height) / 2, Edge, maxY));
        }

        var x = Math.Clamp(t.X, Edge, maxX);

        if (t.Bottom + Gap + card.Height <= host.Height - Edge)
        {
            return new Point(x, t.Bottom + Gap);
        }

        if (t.Top - Gap - card.Height >= Edge)
        {
            return new Point(x, t.Top - Gap - card.Height);
        }

        // Objetivo muy alto (una lista, una tarjeta grande): al lado, donde haya sitio.
        var y = Math.Clamp(t.Top, Edge, maxY);
        if (t.Right + Gap + card.Width <= host.Width - Edge)
        {
            return new Point(t.Right + Gap, y);
        }

        if (t.Left - Gap - card.Width >= Edge)
        {
            return new Point(t.Left - Gap - card.Width, y);
        }

        // Sin hueco: abajo del todo, encima del objetivo pero sin salirse.
        return new Point(x, maxY);
    }
}
