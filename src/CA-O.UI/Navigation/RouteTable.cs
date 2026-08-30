using Microsoft.UI.Xaml.Controls;

namespace CAO.UI.Navigation;

/// <summary>
/// Single source of truth for shell navigation routes (spec 63/138). Tests
/// assert every nav tag resolves to an existing page type.
/// </summary>
public static class RouteTable
{
    public static readonly IReadOnlyDictionary<string, Type> Routes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["dashboard"] = typeof(Pages.DashboardPage),
            ["analyze"] = typeof(Pages.AnalyzePage),
            ["optimize"] = typeof(Pages.OptimizePage),
            ["gaming"] = typeof(Pages.GamingPage),
            ["benchmark"] = typeof(Pages.BenchmarkPage),
            ["restore"] = typeof(Pages.RestorePage),
            ["history"] = typeof(Pages.HistoryPage),
            ["settings"] = typeof(Pages.SettingsPage),
        };

    /// <summary>Ordered tags as shown in the NavigationView.</summary>
    public static readonly IReadOnlyList<string> Order = new[]
    {
        "dashboard", "analyze", "optimize", "gaming",
        "benchmark", "restore", "history", "settings",
    };

    public static Type? Resolve(string? tag) =>
        tag is not null && Routes.TryGetValue(tag, out var pageType) ? pageType : null;
}
