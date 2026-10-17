using System.Text.RegularExpressions;
using CAO.UI.Navigation;
using CAO.UI.Tutorial;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// 2.2.20: los tours de página resaltaban controles que casi siempre están
/// ocultos (InfoBar cerrado, tarjeta "sin datos" colapsada, botón que solo
/// aparece en un caso). El paso salía sin foco y parecía roto. Cada objetivo
/// de cada tour de página tiene que poder verse en el estado normal: no se
/// declara colapsado ni cerrado en el XAML y el code-behind no lo oculta.
/// </summary>
public sealed class PageTourTargetVisibilityTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CA-O.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró CA-O.sln");
    }

    private static string SourceFor(string route, string extension)
    {
        var type = RouteTable.Resolve(route)!;
        var folder = type.Namespace == "CAO.UI.Tutorial"
            ? Path.Combine(RepoRoot(), "src", "CA-O.UI", "Tutorial")
            : Path.Combine(RepoRoot(), "src", "CA-O.UI");
        return Path.Combine(folder, type.Name + extension);
    }

    private static string OpeningTag(string xaml, string name)
    {
        var at = xaml.IndexOf("x:Name=\"" + name + "\"", StringComparison.Ordinal);
        if (at < 0) return string.Empty;
        var open = xaml.LastIndexOf('<', at);
        var close = xaml.IndexOf('>', at);
        return xaml[open..(close + 1)];
    }

    public static TheoryData<string, string> Steps()
    {
        var data = new TheoryData<string, string>();
        foreach (var (route, steps) in PageTourCatalog.ByRoute)
            foreach (var step in steps)
                data.Add(route, step.TargetName);
        return data;
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public void PageTourTargetIsVisibleInTheNormalState(string route, string target)
    {
        var xaml = File.ReadAllText(SourceFor(route, ".xaml"));
        var tag = OpeningTag(xaml, target);
        Assert.False(string.IsNullOrEmpty(tag), $"{route}: '{target}' no existe en el XAML");
        Assert.DoesNotContain("Visibility=\"Collapsed\"", tag, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOpen=\"False\"", tag, StringComparison.Ordinal);

        var codePath = SourceFor(route, ".xaml.cs");
        if (!File.Exists(codePath)) return;
        var code = File.ReadAllText(codePath);
        var hides = new Regex(@"\b" + Regex.Escape(target) + @"\.(Visibility\s*=\s*(?<v>[^;]+);|IsOpen\s*=\s*(?<v>[^;]+);)");
        foreach (Match m in hides.Matches(code))
        {
            var value = m.Groups["v"].Value.Trim();
            Assert.True(value is "Visibility.Visible" or "true",
                $"{route}: '{target}' se oculta en el code-behind ({m.Value.Trim()}); el tour quedaría sin foco.");
        }
    }
}
