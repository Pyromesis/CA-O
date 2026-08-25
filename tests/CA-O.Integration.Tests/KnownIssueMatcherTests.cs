using CAO.Core.Compatibility;
using CAO.Shared;
using Xunit;

namespace CAO.Integration.Tests;

/// <summary>Known-issues matching semantics (spec 55).</summary>
public sealed class KnownIssueMatcherTests
{
    private static readonly SystemContext Context = SystemContextFactory.Default() with
    {
        GpuName = "NVIDIA GeForce RTX 4070",
        GpuDriverVersion = "566.36",
        GamesDetected = ["Counter-Strike 2"],
    };

    [Fact]
    public void SampleEntriesAreNeverSurfaced()
    {
        var database = new[] { SampleIssue() };

        Assert.Empty(KnownIssueMatcher.Match(database, Context));
    }

    [Fact]
    public void DriverScopedIssueRequiresMeasuredDpcPressure()
    {
        var database = new[]
        {
            SampleIssue() with
            {
                Id = "usb-dpc-stutter",
                Status = KnownIssueStatus.Active,
                ComponentKind = "usb",
                SummaryEs = "Driver USB con presión DPC conocida.",
                WorkaroundEs = "Actualice el driver del chipset.",
            },
        };

        // Without measurement there is no evidence: nothing is surfaced.
        Assert.Empty(KnownIssueMatcher.Match(database, Context));

        var underPressure = new InterruptPressureReport(
            TimeSpan.FromSeconds(5), [], TotalMaxDpcPercent: 18, TotalMaxInterruptPercent: 4);
        var finding = Assert.Single(KnownIssueMatcher.Match(database, Context, underPressure));

        Assert.Contains("DPC", finding.MatchReasonEs);
    }

    [Fact]
    public void BuildRangeOutsideContextDoesNotMatch()
    {
        var database = new[]
        {
            SampleIssue() with
            {
                Id = "old-build-issue",
                Status = KnownIssueStatus.Active,
                MinWindowsBuild = 30000,
                MaxWindowsBuild = 31000,
                SummaryEs = "Problema exclusivo de builds futuras.",
                WorkaroundEs = "N/A.",
            },
        };

        Assert.Empty(KnownIssueMatcher.Match(database, Context));
    }

    [Fact]
    public void GameSpecificIssueMatchesOnlyWhenGameIsDetected()
    {
        var database = new[]
        {
            SampleIssue() with
            {
                Id = "cs2-specific",
                Status = KnownIssueStatus.Active,
                GameNameContains = "Counter-Strike",
                SummaryEs = "Problema documentado con CS2 y este driver.",
                WorkaroundEs = "Ajustar el perfil del driver.",
            },
        };

        Assert.Single(KnownIssueMatcher.Match(database, Context));

        var withoutGame = Context with { GamesDetected = [] };
        Assert.Empty(KnownIssueMatcher.Match(database, withoutGame));
    }

    private static KnownIssue SampleIssue() => new()
    {
        Id = "sample",
        SummaryEs = "Entrada de ejemplo para validar el matcher.",
        WorkaroundEs = "Ninguna.",
        Severity = KnownIssueSeverity.Information,
        Status = KnownIssueStatus.Sample,
    };
}
