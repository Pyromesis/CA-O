using System.Text.RegularExpressions;
using Xunit;

namespace CAO.Integration.Tests;

/// <summary>
/// Architecture dependency rules (FASE 36), enforced by source scanning:
///   - the UI never spawns processes nor hosts the privileged pipe;
///   - Core stays free of UI references;
///   - Infrastructure/Privileged stay free of UI references;
///   - Process.Start exists ONLY inside the execution gateway.
/// </summary>
public sealed class ArchitectureDependencyTests
{
    private static string RepoRoot => TestUtils.GetRepoRoot();

    private static IEnumerable<string> SourceFiles(string project) =>
        TestUtils.GetProjectSourceFiles(project);

    [Fact]
    public void UiNeverSpawnsProcessesOrHostsThePipe()
    {
        foreach (var file in SourceFiles("CA-O.UI"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("Process.Start", text);
            Assert.DoesNotContain("new Process", text);
            Assert.DoesNotContain("NamedPipeServerStream", text);
            Assert.DoesNotContain("using CAO.Privileged", text);
            Assert.DoesNotContain("using System.Management", text);
        }
    }

    [Fact]
    public void CoreNeverReferencesUiOrInfrastructure()
    {
        foreach (var file in SourceFiles("CA-O.Core"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("using CAO.UI", text);
            Assert.DoesNotContain("using CAO.Infrastructure", text);
        }
    }

    [Fact]
    public void InfrastructureAndPrivilegedNeverReferenceUi()
    {
        foreach (var project in new[] { "CA-O.Infrastructure", "CA-O.Privileged" })
        {
            foreach (var file in SourceFiles(project))
            {
                Assert.DoesNotContain("using CAO.UI", File.ReadAllText(file));
            }
        }
    }

    [Fact]
    public void ProcessStartLivesOnlyInTheExecutionGateway()
    {
        var offenders = new List<string>();

        foreach (var project in new[] { "CA-O.Shared", "CA-O.Core", "CA-O.Infrastructure", "CA-O.Privileged", "CA-O.UI" })
        {
            foreach (var file in SourceFiles(project))
            {
                var text = File.ReadAllText(file);
                if ((text.Contains("Process.Start") || text.Contains("new Process {")) &&
                    !file.EndsWith("SystemCommandGateway.cs", StringComparison.Ordinal))
                {
                    offenders.Add(file);
                }
            }
        }

        // The gateway is the single allowed location; everything else is a
        // FASE 4 violation.
        var gateway = Path.Combine(RepoRoot, "src", "CA-O.Infrastructure",
            "Windows", "Execution", "SystemCommandGateway.cs");
        Assert.Empty(offenders);
        Assert.True(File.Exists(gateway), "Falta el gateway de ejecución.");
        Assert.Contains("new Process {", File.ReadAllText(gateway));
    }
}
