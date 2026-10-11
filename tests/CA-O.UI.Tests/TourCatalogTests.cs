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
    public void Catalog_has_seven_chapters_each_with_a_summary_and_points()
    {
        Assert.Equal(7, TourCatalog.Chapters.Count);
        Assert.Equal(
            new[] { "analyze", "optimize", "cleanup", "restore", "drivers", "history", "settings" },
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
    /// The tour opens on the Panel, which is a waypoint rather than a topic: the Tutorial
    /// page documents the seven work/reference pages, so "dashboard" is the single step
    /// route with no chapter. Asserted as an exact set difference instead of a loop, so a
    /// NEW step pointing at a chapter-less route still fails here.
    /// </summary>
    [Fact]
    public void The_dashboard_step_is_the_only_tour_step_with_no_chapter()
    {
        var chapters = TourCatalog.Chapters.Select(c => c.Route).ToHashSet();
        var orphanSteps = TourCatalog.Steps
            .Select(s => s.Route)
            .Where(r => !chapters.Contains(r))
            .OrderBy(r => r, StringComparer.Ordinal);

        Assert.Equal(new[] { "dashboard" }, orphanSteps);
    }
}