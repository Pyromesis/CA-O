using CAO.Core.Abstractions;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// CAO-BUG-2026-10-06: <see cref="TempFileCleanupOptimization"/> ya enumeraba con
/// <c>EnumerationOptions</c> (ignora subarboles sin acceso y no sigue puntos de
/// reanillado), pero el resto de limpiezas de cache seguian usando
/// <c>SearchOption.AllDirectories</c> a pelo. Eso significa que un unico
/// directorio sin permiso abortaba la enumeracion COMPLETA (se perdian los
/// ficheros ya listados) y que un junction hacia el padre podia hacer que el
/// recorrido no terminase nunca, sin ningun tiempo maximo que lo corte.
/// </summary>
public sealed class SafeFileEnumerationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cao-safeenum-" + Guid.NewGuid().ToString("N"));

    public SafeFileEnumerationTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void RecursiveOptions_SkipReparsePoints_AndTolerateInaccessibleSubtrees()
    {
        Assert.True(SafeFileEnumeration.RecursiveOptions.RecurseSubdirectories,
            "La enumeracion debe ser recursiva: en %TEMP% y las caches el grueso esta en subcarpetas.");
        Assert.True(SafeFileEnumeration.RecursiveOptions.IgnoreInaccessible,
            "Sin esto, una sola subcarpeta sin acceso aborta el recorrido completo.");
        Assert.True(SafeFileEnumeration.RecursiveOptions.AttributesToSkip.HasFlag(FileAttributes.ReparsePoint),
            "Sin esto, un junction hacia el padre produce un recorrido infinito.");
    }

    [Fact]
    public void Files_WalksNestedDirectories_AndMatchesThePattern()
    {
        var nested = Path.Combine(_dir, "sub", "deeper");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(_dir, "root.tmp"), "x");
        File.WriteAllText(Path.Combine(nested, "inner.tmp"), "y");
        File.WriteAllText(Path.Combine(nested, "keep.log"), "z");

        var found = SafeFileEnumeration.Files(_dir, "*.tmp")
            .Select(path => Path.GetFileName(path) ?? path)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["inner.tmp", "root.tmp"], found);
    }
}