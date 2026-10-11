using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CAO.UI.Navigation;
using CAO.UI.Tutorial;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// Guardia del registro de objetivos del recorrido (spec 4). Dos comprobaciones
/// por paso, y las dos son necesarias:
///
/// 1. EXISTENCIA: el <c>x:Name</c> sigue declarado en el .xaml de la pagina, o
///    un renombrado en la pagina rompe el foco del tour en silencio.
/// 2. VISIBILIDAD: ninguna pagina apaga DIRECTAMENTE su propio objetivo
///    (asignacion sobre el propio nombre en tiempo de ejecucion ni literal
///    en su propia etiqueta XAML). El colapso de ancestros queda fuera de
///    alcance. Sin esta mitad, un objetivo oculto directamente pasaria la
///    existencia y el tour resaltaria un control de tamano cero.
///
/// La segunda comprobacion mira <c>Visibility</c> E <c>IsOpen</c>, porque los
/// <c>InfoBar</c> de este repo se cierran por <c>IsOpen</c> y no por
/// <c>Visibility</c> (casos reales en AnalyzePage.xaml.cs:117-145 y
/// DashboardPage.xaml.cs:240-248). Buscar solo <c>Visibility</c> daria un falso
/// verde. La busqueda es POR PAGINA: un <c>x:Name</c> solo es unico dentro de su
/// pagina y hay colisiones reales (AnalyzePage.xaml.cs modifica OTRO control
/// tambien llamado NoteBar).
///
/// Solo lee archivos .xaml y .xaml.cs; nunca toca
/// %AppData%\CA-O\settings.json ni necesita el seam <c>TourProgress.StoreOverride</c>,
/// asi que esta clase no comparte coleccion con <c>TourProgressTests</c>.
/// </summary>
public sealed class TourTargetRegistryTests
{
    /// <summary>
    /// Asignaciones a <c>objetivo.Visibility</c> / <c>objetivo.IsOpen</c>. El
    /// patron NO exige un punto delante del nombre: RestorePage.xaml.cs:52
    /// escribe <c>SnapshotsList.Visibility = ...</c> con sangria y sin punto, y un
    /// patron con <c>\.</c> inicial no lo encontraria jamas. El limite de palabra
    /// evita que <c>SnapshotsList</c> case con <c>MySnapshotsList</c>.
    /// </summary>
    private static Regex AssignmentFor(string targetName) =>
        new(@"\b" + Regex.Escape(targetName) + @"\s*\.\s*(?:Visibility|IsOpen)\s*=\s*(?<value>[^;]+);",
            RegexOptions.CultureInvariant);

    /// <summary>Literales de colapso declarados en el propio elemento del objetivo.</summary>
    private static readonly string[] HiddenLiterals =
    {
        "Visibility=\"Collapsed\"",
        "Visibility=\"Hidden\"",
        "Visibility=\"hidden\"",
        "Visibility=\"collapsed\"",
        "IsOpen=\"False\"",
        "IsOpen=\"false\"",
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CA-O.sln")))
        {
            dir = dir.Parent;
        }

        // Falla ruidosamente: una guarda que pasa porque no encontro nada es el
        // fallo exacto que ya sufrio esta funcionalidad.
        Assert.True(dir is not null,
            $"No se encontro 'CA-O.sln' subiendo desde '{AppContext.BaseDirectory}'. " +
            "La guardia de objetivos no puede pasar por vacio.");
        return dir!.FullName;
    }

    private static string SourceDirFor(string route)
    {
        var type = RouteTable.Resolve(route);
        Assert.True(type is not null, $"la ruta '{route}' no resuelve en RouteTable.");
        var dir = Path.Combine(RepoRoot(), "src", "CA-O.UI");
        Assert.True(Directory.Exists(dir),
            $"No existe el directorio de paginas '{dir}'. Se esperaba 'src\\CA-O.UI' " +
            "(las paginas NO viven en una subcarpeta Pages; solo el namespace es CAO.UI.Pages).");
        return dir;
    }

    private static string XamlPathFor(string route) => Path.Combine(SourceDirFor(route), RouteTable.Resolve(route)!.Name + ".xaml");

    /// <summary>
    /// Extrae la etiqueta de apertura que contiene el <c>x:Name</c>: desde el '&lt;'
    /// anterior hasta el '&gt;' no citado, respetando comillas para no cortar en un
    /// '&gt;' dentro de un valor de atributo.
    /// </summary>
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
    [InlineData("dashboard", "AnalyzeButton")]
    [InlineData("analyze", "RunButton")]
    [InlineData("optimize", "ApplyRecommendedButton")]
    [InlineData("cleanup", "CleanAllButton")]
    [InlineData("restore", "NoteBar")]
    public void Spotlight_target_exists_in_the_page_xaml(string route, string targetName)
    {
        var xamlPath = XamlPathFor(route);
        Assert.True(File.Exists(xamlPath), $"falta el XAML de la ruta '{route}': {xamlPath}");

        var text = File.ReadAllText(xamlPath);
        var pattern = "x:Name=\"" + Regex.Escape(targetName) + "\"";
        Assert.Matches(pattern, text);
    }

    /// <summary>
    /// El objetivo existe pero podria estar apagado. Falla si la pagina lo colapsa
    /// en tiempo de ejecucion o si el XAML lo declara oculto.
    /// </summary>
    [Theory]
    [InlineData("dashboard", "AnalyzeButton")]
    [InlineData("analyze", "RunButton")]
    [InlineData("optimize", "ApplyRecommendedButton")]
    [InlineData("cleanup", "CleanAllButton")]
    [InlineData("restore", "NoteBar")]
    public void Spotlight_target_is_never_hidden_by_its_page(string route, string targetName)
    {
        var dir = SourceDirFor(route);
        var xamlPath = XamlPathFor(route);
        Assert.True(File.Exists(xamlPath), $"falta el XAML de la ruta '{route}': {xamlPath}");

        // (a) La pagina no puede apagar su propio objetivo en tiempo de ejecucion.
        var codeBehind = Path.Combine(dir, Path.GetFileNameWithoutExtension(xamlPath) + ".xaml.cs");
        Assert.True(File.Exists(codeBehind), $"falta el code-behind de la ruta '{route}': {codeBehind}");

        var code = File.ReadAllText(codeBehind);
        var assignments = AssignmentFor(targetName).Matches(code).Cast<Match>().ToArray();
        foreach (var match in assignments)
        {
            var value = match.Groups["value"].Value.Trim();
            var alwaysVisible = value is "Visibility.Visible" or "true";
            Assert.True(alwaysVisible,
                $"'{targetName}' se oculta en tiempo de ejecucion en " +
                $"{Path.GetFileName(codeBehind)}: '{match.Value.Trim()}'. " +
                "Un objetivo del tour no puede depender de estado; el foco se cairia a un control invisible.");
        }

        // (b) El propio elemento no puede declararse colapsado en el XAML.
        var tag = OpeningTagContaining(File.ReadAllText(xamlPath), targetName);
        Assert.NotEqual(string.Empty, tag);
        foreach (var literal in HiddenLiterals)
        {
            Assert.DoesNotContain(literal, tag, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// El patron de (a) tiene que poder FALLAR. Si el patron se rompe en una
    /// refactorizacion y ya no encuentra nada, las dos comprobaciones de arriba
    /// darian verde por vacio otra vez. Este hecho ata el patron a un caso real
    /// conocido y verificable hoy en el repo: SnapshotsList, el objetivo que el
    /// paso 5 solia usar y que RestorePage.xaml.cs:52 colapsa cuando el usuario
    /// no tiene puntos de restauracion (siempre cierto en una primera ejecucion).
    /// Por eso el paso 5 apunta a NoteBar y no debe revertir.
    /// </summary>
    [Fact]
    public void Visibility_pattern_catches_the_known_SnapshotsList_collapse()
    {
        var codeBehind = Path.Combine(SourceDirFor("restore"), "RestorePage.xaml.cs");
        Assert.True(File.Exists(codeBehind), $"falta el code-behind de restore: {codeBehind}");

        var code = File.ReadAllText(codeBehind);
        var matches = AssignmentFor("SnapshotsList").Matches(code).Cast<Match>().ToArray();

        Assert.NotEmpty(matches);
        Assert.Contains(matches, m => m.Groups["value"].Value.Contains("Collapsed", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_catalog_step_is_covered_by_this_registry()
    {
        var expected = TourCatalog.Steps
            .Select(s => (s.Route, s.TargetName))
            .OrderBy(p => p.Route)
            .ToArray();

        var registered = new (string Route, string TargetName)[]
        {
            ("analyze", "RunButton"),
            ("cleanup", "CleanAllButton"),
            ("dashboard", "AnalyzeButton"),
            ("optimize", "ApplyRecommendedButton"),
            ("restore", "NoteBar"),
        }.OrderBy(p => p.Route).ToArray();

        Assert.Equal(expected, registered);
    }
}