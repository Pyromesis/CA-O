using CAO.UI.Navigation;
using CAO.UI.Tutorial;
using Xunit;

namespace CAO.UI.Tests;

public sealed class TourCatalogTests
{
    [Fact]
    public void Catalog_has_five_steps_in_the_approved_order()
    {
        Assert.Equal(5, TourCatalog.Steps.Count);
        Assert.Equal(
            new[] { "dashboard", "analyze", "optimize", "cleanup", "restore" },
            TourCatalog.Steps.Select(s => s.Route));
        Assert.Equal(
            new[] { "AnalyzeButton", "RunButton", "ApplyRecommendedButton", "CleanAllButton", "NoteBar" },
            TourCatalog.Steps.Select(s => s.TargetName));
    }

    [Fact]
    public void Every_step_targets_a_named_control_and_has_readable_copy()
    {
        foreach (var step in TourCatalog.Steps)
        {
            Assert.False(string.IsNullOrWhiteSpace(step.TargetName));
            Assert.False(string.IsNullOrWhiteSpace(step.Title));
            Assert.False(string.IsNullOrWhiteSpace(step.Body));
            Assert.Contains(step.Mood, new[] { "Idle", "Working", "Celebrate" });
        }
    }

    [Fact]
    public void Every_step_route_resolves_to_a_page_type()
    {
        foreach (var step in TourCatalog.Steps)
        {
            Assert.True(RouteTable.Resolve(step.Route) is not null, $"route '{step.Route}' does not resolve");
        }
    }

    [Fact]
    public void Catalog_has_one_chapter_per_page_each_with_a_summary_and_points()
    {
        // 2.2.20: un capítulo por cada página de la navegación salvo el hub Tutorial.
        Assert.Equal(
            RouteTable.Order.Where(r => r != "tutorial"),
            TourCatalog.Chapters.Select(c => c.Route));
        foreach (var chapter in TourCatalog.Chapters)
        {
            Assert.False(string.IsNullOrWhiteSpace(chapter.Title));
            Assert.False(string.IsNullOrWhiteSpace(chapter.Summary));
            Assert.NotEmpty(chapter.Points);
            Assert.True(RouteTable.Resolve(chapter.Route) is not null, $"chapter route '{chapter.Route}' does not resolve");
        }
    }

    [Fact]
    public void Tutorial_route_resolves_to_the_tutorial_page()
    {
        Assert.Equal(typeof(Tutorial.TutorialPage), RouteTable.Resolve("tutorial"));
        Assert.Contains("tutorial", RouteTable.Order);
    }

    /// <summary>
    /// Cada paso del recorrido general apunta a una página que tiene capítulo: desde
    /// el Tutorial siempre se puede leer el capítulo y lanzar el tour de esa sección.
    /// </summary>
    [Fact]
    public void Every_tour_step_has_a_chapter()
    {
        var chapters = TourCatalog.Chapters.Select(c => c.Route).ToHashSet();
        Assert.Empty(TourCatalog.Steps.Select(s => s.Route).Where(r => !chapters.Contains(r)));
    }

    [Fact]
    public void Every_chapter_has_a_page_tour_and_an_icon()
    {
        foreach (var chapter in TourCatalog.Chapters)
        {
            Assert.NotEmpty(PageTourCatalog.ForRoute(chapter.Route));
            Assert.False(string.IsNullOrWhiteSpace(chapter.Glyph));
        }
    }
}
