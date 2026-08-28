using System.Text;
using Xunit;

namespace CAO.Integration.Tests;

/// <summary>
/// Source-encoding guard (regression): the repo must stay single-encoding
/// UTF-8 (no BOM) and free of mojibake box-drawing sequences that came from
/// mis-decoding accented Spanish. This protects every user-facing string.
/// </summary>
public sealed class EncodingConsistencyTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Concat(Directory.EnumerateFiles(Path.Combine(RepoRoot, "tests"), "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)));

    // Mojibake is produced when UTF-8 accented Spanish is read as a legacy
    // codepage. Every corruption variant shares the box-drawing marker char,
    // so a single presence check over sources is sufficient.

    [Fact]
    public void NoSourceFileContainsMojibakeBoxDrawingSequences()
    {
        var failures = new List<string>();
        foreach (var file in SourceFiles())
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            // The marker char appears in every corruption variant; require it absent.
            const char boxDrawingMarker = (char)0x251C;
            if (text.Contains(boxDrawingMarker))
            {
                failures.Add(Path.GetFileName(file) + ": contiene secuencia mojibake adicional");
            }
        }

        Assert.True(failures.Count == 0,
            "Mojibake detectado en:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void NoSourceFileIsUtf16Encoded()
    {
        var failures = new List<string>();
        foreach (var file in SourceFiles())
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                failures.Add(file + " está codificado como UTF-16 LE (debe ser UTF-8 sin BOM).");
            }
        }

        Assert.True(failures.Count == 0,
            "Archivos con codificación inconsistente:\n" + string.Join("\n", failures));
    }
}
