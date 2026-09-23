using System.IO;
using System.Reflection;

namespace CAO.Integration.Tests;

/// <summary>
/// Shared utilities for integration tests.
/// </summary>
public static class TestUtils
{
    private static string? _cachedRepoRoot;

    /// <summary>
    /// Gets the repository root directory by walking up from the test assembly location
    /// until finding CA-O.sln.
    /// </summary>
    public static string GetRepoRoot()
    {
        if (_cachedRepoRoot != null)
        {
            return _cachedRepoRoot;
        }

        var asm = Assembly.GetExecutingAssembly();
        var codeBase = new Uri(asm.Location).LocalPath;
        var dir = new FileInfo(codeBase).Directory;

        // Subir buscando CA-O.sln sin número fijo de niveles (robusto ante
        // cambios de layout bin/, testhost o single-file), con tope de 12.
        for (int i = 0; i < 12 && dir != null; i++)
        {
            if (dir.GetFiles("CA-O.sln").Length > 0)
            {
                _cachedRepoRoot = dir.FullName;
                return _cachedRepoRoot;
            }
            dir = dir.Parent;
        }
        // Fallback: subir desde el directorio actual.
        var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (cwd != null && cwd.GetFiles("CA-O.sln").Length == 0)
        {
            cwd = cwd.Parent;
        }
        if (cwd != null && cwd.GetFiles("CA-O.sln").Length > 0)
        {
            _cachedRepoRoot = cwd.FullName;
            return _cachedRepoRoot;
        }
        throw new DirectoryNotFoundException("Cannot find repo root (CA-O.sln not found)");
    }
    
    /// <summary>
    /// Gets all C# source files in the repository (src/ and tests/ directories).
    /// </summary>
    public static IEnumerable<string> GetAllSourceFiles()
    {
        var repoRoot = GetRepoRoot();
        var srcDir = Path.Combine(repoRoot, "src");
        var testsDir = Path.Combine(repoRoot, "tests");
        
        foreach (var dir in new[] { srcDir, testsDir })
        {
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    // Igual que GetProjectSourceFiles: fuera generados obj/bin.
                    if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    {
                        continue;
                    }
                    yield return file;
                }
            }
        }
    }
    
    /// <summary>
    /// Gets all C# source files for a specific project.
    /// </summary>
    public static IEnumerable<string> GetProjectSourceFiles(string projectName)
    {
        var repoRoot = GetRepoRoot();
        var projectDir = Path.Combine(repoRoot, "src", projectName);
        
        if (!Directory.Exists(projectDir))
        {
            projectDir = Path.Combine(repoRoot, "tests", projectName);
        }
        
        if (Directory.Exists(projectDir))
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (!file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                    !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    yield return file;
                }
            }
        }
    }
    
    /// <summary>
    /// Reads a file from the repository root.
    /// </summary>
    public static string ReadRepoFile(params string[] parts)
    {
        var repoRoot = GetRepoRoot();
        var path = Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }
}