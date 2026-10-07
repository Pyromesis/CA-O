using Xunit;

namespace CAO.Integration.Tests;

/// <summary>
/// CAO-BUG-2026-10-06: <c>SearchOption.AllDirectories</c> a pelo aborta la
/// enumeracion entera en el primer directorio sin acceso y sigue los puntos de
/// reanillado, de modo que un junction hacia el padre produce un recorrido
/// infinito. El primer sitio donde se corrigio fue
/// <c>TempFileCleanupOptimization</c>, pero el resto de limpiezas de cache
/// seguian usandolo a pelo. Esta prueba es el cerrojo: impide reintroducirlo.
/// </summary>
public sealed class RecursiveEnumerationGuardTests
{
    [Fact]
    public void NoOptimizationEnumeratesRecursivelyWithoutEnumerationOptions()
    {
        var offenders = new List<string>();

        foreach (var file in TestUtils.GetProjectSourceFiles("CA-O.Core"))
        {
            // La familia de temporales ya usa EnumerationOptions; su comentario
            // historico menciona el termino para explicar por que se cambio.
            if (file.EndsWith("TempFileCleanupOptimization.cs", StringComparison.Ordinal)) continue;

            // Exencion explicita: OptimizationEngine cuenta ficheros .inf dentro
            // de una carpeta de driver que CA-O acaba de extraer o crear (bajo
            // ProgramData\CA-O\DriverBackup o el temp propio). El arbol es
            // nuestro y acotado, sin reenlaces que un tercero pueda colocar, asi
            // que la enumeracion cruda no tiene los dos riesgos que motiva el veto.
            if (file.EndsWith("OptimizationEngine.cs", StringComparison.Ordinal)) continue;

            var relative = Path.GetFileName(file);
            var text = File.ReadAllText(file);

            // Solo importan las llamadas reales, no los comentarios ni los docs.
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith('*')
                    || trimmed.StartsWith("///", StringComparison.Ordinal))
                    continue;

                if (line.Contains("SearchOption.AllDirectories", StringComparison.Ordinal)
                    || (line.Contains("GetFiles(", StringComparison.Ordinal)
                        && line.Contains("AllDirectories", StringComparison.Ordinal)))
                {
                    offenders.Add(relative + ": " + trimmed);
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Enumeraciones recursivas sin EnumerationOptions (aborta con subarboles sin acceso y sigue junctions):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }
}