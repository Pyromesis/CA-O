using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CAO.UI;
using CAO.UI.Navigation;
using CAO.UI.Tutorial;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// Tour por pagina (PageTourCatalog): una lista de 6-10 pasos por cada ruta de
/// <c>RouteTable.Order</c> salvo "tutorial", que es el hub y queda excluida.
/// </summary>
public sealed class PageTourCatalogTests
{
    private static readonly string[] ExpectedRoutes =
    {
        "dashboard", "analyze", "optimize", "cleanup", "solucionar", "drivers",
        "benchmark", "overclock", "restore", "history", "settings",
    };

    [Fact]
    public void ByRoute_covers_every_non_hub_route_exactly()
    {
        var actual = PageTourCatalog.ByRoute.Keys.OrderBy(r => r, StringComparer.Ordinal).ToArray();
        var expected = ExpectedRoutes.OrderBy(r => r, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Tutorial_hub_is_excluded()
    {
        Assert.DoesNotContain("tutorial", PageTourCatalog.ByRoute.Keys);
        Assert.Empty(PageTourCatalog.ForRoute("tutorial"));
        Assert.Empty(PageTourCatalog.ForRoute("no-existe"));
        Assert.Empty(PageTourCatalog.ForRoute(null));
    }

    [Fact]
    public void Every_page_has_between_6_and_10_steps()
    {
        foreach (var route in ExpectedRoutes)
        {
            var steps = PageTourCatalog.ForRoute(route);
            Assert.InRange(steps.Count, 6, 10);
        }
    }

    [Fact]
    public void Every_step_targets_its_own_page_with_readable_copy()
    {
        foreach (var (route, steps) in PageTourCatalog.ByRoute)
        {
            foreach (var step in steps)
            {
                Assert.Equal(route, step.Route);
                Assert.True(RouteTable.Resolve(step.Route) is not null, $"route '{step.Route}' does not resolve");
                Assert.False(string.IsNullOrWhiteSpace(step.TargetName));
                Assert.False(string.IsNullOrWhiteSpace(step.Title));
                Assert.False(string.IsNullOrWhiteSpace(step.Body));
                Assert.Contains(step.Mood, new[] { "Idle", "Working", "Celebrate" });
            }
        }
    }

    [Fact]
    public void No_step_reuses_the_general_tour_button_targets_outside_its_page()
    {
        // Each page-tour step must point at its own route: a step pointing elsewhere
        // would yank the user out of the page mid-tour.
        foreach (var (route, steps) in PageTourCatalog.ByRoute)
        {
            Assert.All(steps, s => Assert.Equal(route, s.Route));
        }
    }

    [Theory]
    [InlineData("tour.page.button")]
    [InlineData("tour.page.tip")]
    public void Page_tour_button_labels_resolve_in_both_languages(string key)
    {
        Localizer.SetLanguage("es-ES");
        try
        {
            Assert.NotEqual(key, Localizer.Get(key));
            Localizer.SetLanguage("en-US");
            Assert.NotEqual(key, Localizer.Get(key));
        }
        finally
        {
            Localizer.SetLanguage("es-ES");
        }
    }
}

/// <summary>
/// Escaneo estatico como <c>TuningTemplate_BindsDescription</c>: cada TargetName del
/// tour por pagina existe como <c>x:Name</c> en el XAML de su pagina, cada pagina
/// declara su boton <c>PageTourButton</c> (sin texto hardcodeado: va por Localizer)
/// y cada code-behind cablea <c>OnPageTourClick</c> con su ruta.
/// </summary>
public sealed class PageTourXamlTests
{
    public static IEnumerable<object[]> AllPageSteps() =>
        PageTourCatalog.ByRoute.SelectMany(kv =>
            kv.Value.Select(s => new object[] { kv.Key, s.TargetName }));

    public static IEnumerable<object[]> AllPagesWithFiles() =>
        PageTourCatalog.ByRoute.Keys.Select(r => new object[]
        {
            r,
            RouteTable.Resolve(r)!.Name + ".xaml",
            RouteTable.Resolve(r)!.Name + ".xaml.cs",
        });

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "CA-O.sln"))) return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("CA-O.sln not found above test binaries.");
    }

    private static string UiDir() => Path.Combine(RepoRoot(), "src", "CA-O.UI");

    private static string OpeningTagContaining(string xaml, string targetName)
    {
        var name = xaml.IndexOf("x:Name=\"" + targetName + "\"", StringComparison.Ordinal);
        if (name < 0) return string.Empty;

        var open = xaml.LastIndexOf('<', name);
        if (open < 0) return string.Empty;

        var inSingle = false;
        var inDouble = false;
        for (var i = open + 1; i < xaml.Length; i++)
        {
            var c = xaml[i];
            if (c == '"' && !inSingle) { inDouble = !inDouble; continue; }
            if (c == '\'' && !inDouble) { inSingle = !inSingle; continue; }
            if (c == '>' && !inSingle && !inDouble) return xaml[open..(i + 1)];
        }

        return string.Empty;
    }

    [Theory]
    [MemberData(nameof(AllPageSteps))]
    public void Spotlight_target_exists_in_the_page_xaml(string route, string targetName)
    {
        var xamlPath = Path.Combine(UiDir(), RouteTable.Resolve(route)!.Name + ".xaml");
        Assert.True(File.Exists(xamlPath), $"falta el XAML de la ruta '{route}': {xamlPath}");

        var text = File.ReadAllText(xamlPath);
        Assert.Contains("x:Name=\"" + targetName + "\"", text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllPagesWithFiles))]
    public void Every_page_declares_a_localized_page_tour_button(string route, string xamlFile, string _)
    {
        var xamlPath = Path.Combine(UiDir(), xamlFile);
        Assert.True(File.Exists(xamlPath), $"falta el XAML de la ruta '{route}': {xamlPath}");

        var text = File.ReadAllText(xamlPath);
        var tag = OpeningTagContaining(text, "PageTourButton");
        Assert.NotEqual(string.Empty, tag);
        Assert.StartsWith("<Button", tag, StringComparison.Ordinal);
        Assert.Contains("OnPageTourClick", tag, StringComparison.Ordinal);
        Assert.Contains("CaoElevatedButtonStyle", tag, StringComparison.Ordinal);
        // Sin texto hardcodeado: el Content se fija en el code-behind via Localizer (spec 84).
        Assert.DoesNotContain("Content=", tag, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllPagesWithFiles))]
    public void Every_page_wires_the_click_handler_with_its_route(string route, string _, string codeFile)
    {
        var codePath = Path.Combine(UiDir(), codeFile);
        Assert.True(File.Exists(codePath), $"falta el code-behind de la ruta '{route}': {codePath}");

        var code = File.ReadAllText(codePath);
        Assert.Contains("OnPageTourClick", code, StringComparison.Ordinal);
        Assert.Contains("TourCoordinator.IsActive", code, StringComparison.Ordinal);
        Assert.Contains("StartPageTour(\"" + route + "\")", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_covers_every_page_catalog_step()
    {
        var expected = PageTourCatalog.ByRoute
            .SelectMany(kv => kv.Value.Select(s => (s.Route, s.TargetName)))
            .OrderBy(p => p.Route, StringComparer.Ordinal)
            .ThenBy(p => p.TargetName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(84, expected.Length);
        Assert.All(expected, p => Assert.NotEqual("tutorial", p.Route));
    }
}

/// <summary>
/// Estado y decision de navegacion del tour por pagina: <c>TourState.StartWith</c>
/// acepta listas arbitrarias y <c>TourCoordinator.NeedsNavigation</c> evita navegar
/// cuando el paso ya esta en pantalla. Todo puro, sin WinUI.
/// </summary>
public sealed class PageTourStateTests
{
    private static TourState StartedWith(IReadOnlyList<TourStep> steps)
    {
        var state = new TourState();
        state.StartWith(steps);
        return state;
    }

    [Fact]
    public void StartWith_accepts_an_arbitrary_list()
    {
        var steps = new[]
        {
            new TourStep("drivers", "ScanButton", "Uno", "Primero", "Idle"),
            new TourStep("drivers", "DriversList", "Dos", "Segundo", "Celebrate"),
        };

        var state = StartedWith(steps);

        Assert.True(state.IsRunning);
        Assert.Equal(2, state.Count);
        Assert.Equal("ScanButton", state.Current!.TargetName);
        Assert.True(state.Next());
        Assert.Equal("DriversList", state.Current!.TargetName);
        Assert.False(state.Next());
        Assert.True(state.IsFinished);
    }

    [Fact]
    public void StartWith_ignores_empty_lists()
    {
        var state = new TourState();
        state.StartWith(Array.Empty<TourStep>());

        Assert.False(state.IsRunning);
        Assert.Null(state.Current);
    }

    [Fact]
    public void Start_restores_the_general_tour_after_a_page_tour()
    {
        var state = StartedWith(PageTourCatalog.ForRoute("drivers"));
        Assert.Equal(PageTourCatalog.ForRoute("drivers").Count, state.Count);

        state.Reset();
        state.Start();

        Assert.Equal(TourCatalog.Steps.Count, state.Count);
        Assert.Equal("AnalyzeButton", state.Current!.TargetName);
    }

    [Fact]
    public void Reset_restores_the_general_catalog()
    {
        var state = StartedWith(PageTourCatalog.ForRoute("settings"));
        state.Reset();
        state.Start();

        Assert.Equal(5, state.Count);
    }

    [Theory]
    [InlineData("analyze", "analyze", false)]
    [InlineData("dashboard", "analyze", true)]
    [InlineData(null, "analyze", true)]
    [InlineData("analyze", "no-existe", true)]
    public void NeedsNavigation_only_navigates_on_route_change(
        string? currentRoute, string stepRoute, bool expected)
    {
        Assert.Equal(expected, TourCoordinator.NeedsNavigation(currentRoute, stepRoute));
    }

    [Fact]
    public void CurrentRouteTagFor_resolves_page_types_back_to_routes()
    {
        Assert.Equal("dashboard", TourCoordinator.CurrentRouteTagFor(typeof(CAO.UI.Pages.DashboardPage)));
        Assert.Equal("cleanup", TourCoordinator.CurrentRouteTagFor(typeof(CAO.UI.Pages.LimpiezaPage)));
        Assert.Equal("solucionar", TourCoordinator.CurrentRouteTagFor(typeof(CAO.UI.Pages.SolucionarPage)));
        Assert.Null(TourCoordinator.CurrentRouteTagFor(null));
        Assert.Null(TourCoordinator.CurrentRouteTagFor(typeof(string)));
    }
}
