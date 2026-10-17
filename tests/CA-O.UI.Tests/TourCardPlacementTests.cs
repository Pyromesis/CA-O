using CAO.UI.Tutorial;
using Windows.Foundation;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>La tarjeta del tour nunca tapa el control resaltado ni se sale de la ventana.</summary>
public sealed class TourCardPlacementTests
{
    private static readonly Size Host = new(1200, 800);
    private static readonly Size Card = new(400, 220);

    private static bool Overlaps(Point p, Rect target) =>
        new Rect(p.X, p.Y, Card.Width, Card.Height) is var card &&
        card.X < target.Right && card.Right > target.X && card.Y < target.Bottom && card.Bottom > target.Y;

    private static void AssertInside(Point p)
    {
        Assert.InRange(p.X, 0, Host.Width - Card.Width);
        Assert.InRange(p.Y, 0, Host.Height - Card.Height);
    }

    [Fact]
    public void Goes_below_a_target_near_the_top()
    {
        var target = new Rect(100, 80, 200, 40);
        var p = TourCardPlacement.Place(target, Card, Host);
        Assert.True(p.Y >= target.Bottom);
        Assert.False(Overlaps(p, target));
        AssertInside(p);
    }

    [Fact]
    public void Goes_above_a_target_near_the_bottom()
    {
        var target = new Rect(700, 700, 300, 50);
        var p = TourCardPlacement.Place(target, Card, Host);
        Assert.True(p.Y + Card.Height <= target.Top);
        Assert.False(Overlaps(p, target));
        AssertInside(p);
    }

    [Fact]
    public void Goes_beside_a_tall_target()
    {
        var target = new Rect(40, 40, 500, 720);
        var p = TourCardPlacement.Place(target, Card, Host);
        Assert.False(Overlaps(p, target));
        AssertInside(p);
    }

    [Fact]
    public void Centres_without_a_target()
    {
        var p = TourCardPlacement.Place(null, Card, Host);
        Assert.Equal((Host.Width - Card.Width) / 2, p.X);
        Assert.Equal((Host.Height - Card.Height) / 2, p.Y);
    }

    [Fact]
    public void Clamps_a_target_at_the_right_edge()
    {
        var p = TourCardPlacement.Place(new Rect(1100, 100, 80, 30), Card, Host);
        AssertInside(p);
    }
}
