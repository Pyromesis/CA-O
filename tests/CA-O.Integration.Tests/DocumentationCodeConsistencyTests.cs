using System.Text.RegularExpressions;
using CAO.Core.Catalog;
using CAO.Shared;
using Xunit;

namespace CAO.Integration.Tests;

/// <summary>
/// Documentation-code consistency (spec 77): the docs may never describe an
/// architecture, engine or catalog entry that is not physically present in
/// the repository. This test FAILS when docs drift from code.
/// </summary>
public sealed class DocumentationCodeConsistencyTests
{
    private static string RepoRoot => TestUtils.GetRepoRoot();

    private static string ReadRepoFile(params string[] parts) =>
        TestUtils.ReadRepoFile(parts);

    [Fact]
    public void EveryCatalogIdInDocsMatchesTheCompiledCatalog()
    {
        var docText = File.ReadAllText(Path.Combine(RepoRoot, "docs", "OPTIMIZATION-CATALOG.md"));
        var documentedIds = Regex.Matches(docText, @"\| ([a-z0-9-]+) \|")
            .Select(match => match.Groups[1].Value)
            .Where(id => id != "id") // header row
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var compiledIds = OptimizationCatalog.All
            .Select(optimization => optimization.Definition.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(documentedIds);
        foreach (var id in documentedIds)
        {
            Assert.True(compiledIds.Contains(id),
                $"docs/OPTIMIZATION-CATALOG.md documenta '{id}' pero no existe en el catálogo compilado.");
        }
    }

    /// <summary>Banned ids (spec 75/100): must never appear as catalog entries.</summary>
    public static TheoryData<string> BannedOptimizationIds => new(
        new[]
        {
            "memory-compression", "disable-cpu-idle", "disable-power-throttling",
            "disable-core-parking", "static-pagefile", "delete-prefetch",
            "winsock-reset", "reset-network", "network-throttling-index-hack",
            "svchost-split-threshold-hack", "cpu-min-state-100",
        });

    [Theory]
    [MemberData(nameof(BannedOptimizationIds))]
    public void CatalogNeverContainsBannedDefaults(string bannedId)
    {
        Assert.DoesNotContain(OptimizationCatalog.All,
            optimization => optimization.Definition.Id.Equals(bannedId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoStaticFpsClaimsAnywhereInUserFacingDocs()
    {
        foreach (var doc in new[] { "README.md", Path.Combine("docs", "OPTIMIZATION-CATALOG.md"), Path.Combine("docs", "ARCHITECTURE.md") })
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot, doc));
            Assert.DoesNotContain("+15 FPS", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("+20 FPS", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("guaranteed FPS", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReferencedProjectsExistOnDisk()
    {
        var architecture = File.ReadAllText(Path.Combine(RepoRoot, "docs", "ARCHITECTURE.md"));
        var mentioned = Regex.Matches(architecture, "CA-O\\.[A-Z][A-Za-z]+(\\.Tests)?")
            .Select(match => match.Value)
            .Where(name => name != "CA-O.sln")
            .Distinct(StringComparer.Ordinal);

        foreach (var project in mentioned)
        {
            var isTestProject = project.EndsWith("Tests", StringComparison.Ordinal);
            var root = isTestProject ? "tests" : "src";
            Assert.True(Directory.Exists(Path.Combine(RepoRoot, root, project)),
                $"ARCHITECTURE.md menciona el proyecto '{project}' que no existe en {root}/.");
        }
    }

    [Fact]
    public void EngineMentionsMapToRealTypes()
    {
        var readme = File.ReadAllText(Path.Combine(RepoRoot, "README.md"));
        // Each engine the README claims must have a real source file.
        var required = new (string Claim, string[] Evidence)[]
        {
            ("Benchmark", new[] { Path.Combine("src", "CA-O.Infrastructure", "Benchmarking", "SystemBenchmarkRunner.cs") }),
            ("Crash recovery", new[] { Path.Combine("src", "CA-O.Core", "Rollback", "CrashRecoveryService.cs") }),
            ("Named Pipes", new[] { Path.Combine("src", "CA-O.Privileged", "PrivilegedPipeService.cs") }),
        };

        foreach (var (claim, evidence) in required)
        {
            if (!readme.Contains(claim, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var file in evidence)
            {
                Assert.True(File.Exists(Path.Combine(RepoRoot, file)),
                    $"El README menciona '{claim}' pero falta su implementación: {file}");
            }
        }
    }

    [Fact]
    public void HealthScoreNeverDependsOnTweakCount()
    {
        // Spec 100: grep-guard the invariant at the documentation level too.
        var architecture = ReadRepoFile("docs", "ARCHITECTURE.md");
        Assert.Contains("nunca depende", architecture, StringComparison.OrdinalIgnoreCase);
    }
}
