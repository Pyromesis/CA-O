using System;
using System.IO;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

public sealed class OverclockTuningVisualTests
{
    [Theory]
    [InlineData(true, true, "Working")]
    [InlineData(true, false, "Working")]
    [InlineData(false, true, "Celebrate")]
    [InlineData(false, false, "Idle")]
    public void MoodForTuning_MapsProgressToValidMoods(bool running, bool validated, string expected) =>
        Assert.Equal(expected, OverclockViewModel.MoodForTuning(running, validated));

    [Fact]
    public void MoodForTuning_UnknownOutcome_MapsToIdle() =>
        Assert.Equal("Idle", OverclockViewModel.MoodForTuning(false, null));

    [Fact]
    public void TuningRow_CarriesDescriptionForTheTemplate()
    {
        var row = new OverclockTuningRow("k.test", "Etiqueta", "0 - 100", null!, "Que hace el ajuste");
        Assert.Equal("Que hace el ajuste", row.Description);
    }

    [Fact]
    public void TuningTemplate_BindsDescription()
    {
        string repo = FindRepoRoot();
        string xaml = File.ReadAllText(Path.Combine(repo, "src", "CA-O.UI", "OverclockPage.xaml"));
        int items = xaml.IndexOf("ViewModel.TuningTargets", StringComparison.Ordinal);
        Assert.True(items >= 0);
        int end = xaml.IndexOf("</ItemsControl>", items, StringComparison.Ordinal);
        Assert.True(end > items);
        Assert.Contains("Text=\"{x:Bind Description}\"", xaml.Substring(items, end - items), StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "CA-O.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("CA-O.sln not found above test binaries.");
    }
}
