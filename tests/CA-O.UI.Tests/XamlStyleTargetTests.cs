using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// Guarda de regresion del bug de Overclock en 2.2.4: OverclockPage.xaml
/// aplicaba CaoHeaderAccentBarStyle (TargetType=Border) a un Rectangle. El
/// compilador XAML no comprueba que el TargetType de un StaticResource
/// coincida con el elemento, asi que la pagina compilaba bien y moria en
/// tiempo de ejecucion dentro de InitializeComponent con XamlParseException;
/// el Frame no navegaba y el contenido se quedaba en la pagina anterior con
/// el menu marcando Overclock. Esta guarda parsea los fuentes XAML y falla
/// en la suite en vez de en la maquina del usuario.
/// </summary>
public sealed class XamlStyleTargetTests
{
    [Fact]
    public void OverclockPage_HeaderBar_IsABorderNotARectangle()
    {
        var xaml = File.ReadAllText(Path.Combine(UiDir(), "OverclockPage.xaml"));

        Assert.DoesNotContain("<Rectangle", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "<Border Style=\"{StaticResource CaoHeaderAccentBarStyle}\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPageStyleUsage_MatchesItsTargetType()
    {
        var targets = StyleTargets();
        var offenders = new List<string>();

        foreach (var page in PageFiles())
        {
            foreach (var (element, key) in StyleUsages(page))
            {
                if (!targets.TryGetValue(key, out var expected))
                {
                    continue;
                }

                if (!string.Equals(element, expected, StringComparison.Ordinal))
                {
                    offenders.Add(
                        $"{Path.GetFileName(page)}: <{element}> usa '{key}' (TargetType={expected}).");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Estilos aplicados a un tipo que no es su TargetType:" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void TheGuard_ActuallySeesStylesAndPages()
    {
        var targets = StyleTargets();
        Assert.True(targets.Count > 0,
            "DesignTokens.xaml no aporto ningun estilo: la guarda pasaria por vacio.");

        Assert.True(
            targets.TryGetValue("CaoHeaderAccentBarStyle", out var bar) && bar == "Border",
            "Se esperaba CaoHeaderAccentBarStyle con TargetType=Border.");

        var pages = PageFiles();
        Assert.True(pages.Count > 0, "No se encontro ninguna pagina .xaml.");
        Assert.Contains(pages, p =>
            string.Equals(Path.GetFileName(p), "OverclockPage.xaml", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------- helpers

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CA-O.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            $"No se encontro 'CA-O.sln' subiendo desde '{AppContext.BaseDirectory}'. "
            + "Una guarda que pasa por vacio es exactamente el fallo que prohibe este test.");
        return dir!.FullName;
    }

    private static string UiDir() => Path.Combine(RepoRoot(), "src", "CA-O.UI");

    private static List<string> PageFiles() =>
        Directory.GetFiles(UiDir(), "*.xaml", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    private static Dictionary<string, string> StyleTargets()
    {
        var doc = XDocument.Load(
            Path.Combine(UiDir(), "Resources", "DesignTokens.xaml"),
            LoadOptions.None);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var style in doc.Descendants().Where(e => e.Name.LocalName == "Style"))
        {
            var key = style.Attributes().FirstOrDefault(a => a.Name.LocalName == "Key")?.Value;
            var target = style.Attributes().FirstOrDefault(a => a.Name.LocalName == "TargetType")?.Value;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            map[key!] = LocalName(target!);
        }

        return map;
    }

    private static List<(string Element, string Key)> StyleUsages(string pagePath)
    {
        var doc = XDocument.Load(pagePath, LoadOptions.None);
        var usages = new List<(string, string)>();

        foreach (var element in doc.Descendants())
        {
            var style = element.Attributes()
                .FirstOrDefault(a => a.Name.LocalName == "Style" && a.Name.Namespace == XNamespace.None)
                ?.Value;
            if (style is null || !style.StartsWith("{StaticResource ", StringComparison.Ordinal))
            {
                continue;
            }

            var key = style.Substring("{StaticResource ".Length).TrimEnd('}', ' ');
            if (key.Length == 0)
            {
                continue;
            }

            usages.Add((element.Name.LocalName, key));
        }

        return usages;
    }

    private static string LocalName(string typeName)
    {
        var cut = typeName.LastIndexOf(':');
        return cut >= 0 ? typeName.Substring(cut + 1) : typeName;
    }
}
