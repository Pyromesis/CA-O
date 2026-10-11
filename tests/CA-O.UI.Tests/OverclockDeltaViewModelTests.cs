using CAO.Core.Overclock.OverclockCatalog;
using CAO.Core.Overclock.Reporting;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Overclock;
using CAO.Infrastructure.Overclock.Reporting;
using CAO.Shared;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// The delta section is the one place where CA-O can quietly start lying:
/// a percentage with no baseline, a regression buried under a win, or a knob
/// credited with a share of a package that moved as a whole.
/// </summary>
public sealed class OverclockDeltaViewModelTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Candidate_WithoutABaseline_RefusesToProduceNumbers()
    {
        var store = new InMemoryDeltaStore();
        var viewModel = ViewModel(new ScriptedDeltaSource(Sample(500)), store);

        await viewModel.CaptureCandidateCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.DeltaRows);
        Assert.Empty(store.Saved);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusLine));
    }

    [Fact]
    public async Task Candidate_AfterABaseline_ProducesOneRowPerAxis()
    {
        var viewModel = ViewModel(new ScriptedDeltaSource(Sample(500), Sample(600)));
        await Measure(viewModel);

        Assert.Equal(4, viewModel.DeltaRows.Count);
        Assert.Equal(4, viewModel.DeltaRows.Select(r => r.Axis).Distinct().Count());
    }

    [Fact]
    public async Task Candidate_NeverAttributesADeltaToAKnob()
    {
        var viewModel = ViewModel(new ScriptedDeltaSource(Sample(500), Sample(600)));
        await Measure(viewModel);

        // The knobs are not independent: more power lifts the memory clock
        // ceiling while eating thermal headroom. Splitting the delta across
        // them would be arithmetic dressed up as measurement.
        var knobIds = OverclockKnobIds();
        var text = string.Join(" | ", viewModel.DeltaRows.Select(r => $"{r.Axis} {r.Baseline} {r.Candidate} {r.Delta} {r.Verdict} {r.Reason}"));
        Assert.DoesNotContain(knobIds, id => text.Contains(id, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Candidate_ReportsTheChangeItMeasured()
    {
        var viewModel = ViewModel(new ScriptedDeltaSource(Sample(500), Sample(600)));
        await Measure(viewModel);

        var cpu = viewModel.DeltaRows.Single(r => r.Axis == Localizer.Get("overclock-axis-cpu"));
        Assert.Contains("+20", cpu.Delta, StringComparison.Ordinal);
        Assert.NotEqual(cpu.Verdict, Localizer.Get("overclock-delta-verdict-no-data"));
    }

    [Fact]
    public async Task Candidate_WithNothingOutsideTheFloor_IsNotCalledAnImprovement()
    {
        var viewModel = ViewModel(new ScriptedDeltaSource(Sample(500), Sample(502)));
        await Measure(viewModel);

        Assert.NotEqual(Localizer.Get("overclock-delta-verdict-improvement"), viewModel.ResultSummary);
    }

    [Fact]
    public async Task Candidate_WithARegression_IsNotCalledAnImprovement()
    {
        var viewModel = ViewModel(new ScriptedDeltaSource(Sample(500, 500, 500, 500), Sample(600, 500, 300, 500)));
        await Measure(viewModel);

        Assert.Equal(Localizer.Get("overclock-delta-verdict-regression"), viewModel.ResultSummary);
    }

    [Fact]
    public async Task Candidate_SavesTheReportSoReopeningThePageKeepsIt()
    {
        var store = new InMemoryDeltaStore();
        var viewModel = ViewModel(new ScriptedDeltaSource(Sample(500), Sample(600)), store);

        await Measure(viewModel);

        var saved = Assert.Single(store.Saved);
        Assert.True(saved.HasData);
    }

    [Fact]
    public async Task ANewBaseline_DiscardsTheComparisonMadeAgainstTheOldOne()
    {
        var viewModel = ViewModel(new ScriptedDeltaSource(Sample(500), Sample(600), Sample(700)));
        await Measure(viewModel);
        Assert.NotEmpty(viewModel.DeltaRows);

        await viewModel.CaptureBaselineCommand.ExecuteAsync(null);

        // A verdict against a baseline the user can no longer reproduce is a
        // number that cannot be checked.
        Assert.Empty(viewModel.DeltaRows);
        Assert.Equal(Localizer.Get("overclock-result-pending"), viewModel.ResultSummary);
    }

    [Fact]
    public async Task Refresh_ShowsTheLastSavedComparisonWhenNothingWasMeasuredYet()
    {
        var store = new InMemoryDeltaStore();
        var measured = ViewModel(new ScriptedDeltaSource(Sample(500), Sample(600)), store);
        await Measure(measured);

        var reopened = ViewModel(new ThrowingDeltaSource(), store);
        await reopened.RefreshCommand.ExecuteAsync(null);

        Assert.NotEmpty(reopened.DeltaRows);
    }

    [Fact]
    public async Task Refresh_IgnoresASavedReportThatHasNoData()
    {
        var store = new InMemoryDeltaStore { Saved = { OverclockDeltaReport.Empty } };
        var viewModel = ViewModel(new ThrowingDeltaSource(), store);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.DeltaRows);
        Assert.Equal(Localizer.Get("overclock-result-pending"), viewModel.ResultSummary);
    }

    [Fact]
    public void Page_WiresTheDeltaRowsAndBothCaptureButtons()
    {
        var xaml = ReadRepoFile("src", "CA-O.UI", "OverclockPage.xaml");

        Assert.Contains("OverclockDeltaRow", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.DeltaRows", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CaptureBaselineCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CaptureCandidateCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDeltaKeyIsTranslatedInBothLanguages()
    {
        var source = ReadRepoFile("src", "CA-O.UI", "Resources", "Localizer.cs");

        foreach (var key in DeltaKeys)
        {
            var occurrences = source.Split($"[\"{key}\"]").Length - 1;
            Assert.True(occurrences >= 2, $"'{key}' appears {occurrences} time(s); both languages need it");
        }
    }

    [Fact]
    public void DeltaSource_ImplementsTheInterfaceTheViewModelIsWrittenAgainst()
    {
        // Guards the seam itself: if CaptureAsync is ever renamed, the page
        // stops compiling in a way this assertion explains.
        var method = typeof(IOverclockDeltaSource).GetMethod(nameof(IOverclockDeltaSource.CaptureAsync));
        Assert.NotNull(method);
        Assert.Equal(typeof(ValueTask<DeltaSample>), method!.ReturnType);
    }

    [Fact]
    public void DeltaStore_IsReachableThroughTheInterfaceTheViewModelIsWrittenAgainst()
    {
        Assert.True(typeof(IOverclockDeltaStore).IsAssignableFrom(typeof(FileOverclockDeltaStore)));
    }

    private static readonly string[] DeltaKeys =
    [
        "overclock-delta-explainer",
        "overclock-delta-floor",
        "overclock-delta-capture-baseline",
        "overclock-delta-capture-candidate",
        "overclock-delta-running",
        "overclock-delta-baseline-captured",
        "overclock-delta-baseline-done",
        "overclock-delta-candidate-done",
        "overclock-delta-baseline-missing",
        "overclock-delta-failed",
        "overclock-delta-save-failed",
        "overclock-delta-unavailable",
        "overclock-delta-percent",
        "overclock-delta-no-data",
        "overclock-delta-zero-baseline",
        "overclock-delta-unit-mismatch",
        "overclock-delta-verdict-improvement",
        "overclock-delta-verdict-no-change",
        "overclock-delta-verdict-regression",
        "overclock-delta-verdict-no-data",
        "overclock-axis-cpu",
        "overclock-axis-memory",
        "overclock-axis-storage-read",
        "overclock-axis-storage-write",
        "overclock-unit-score",
        "overclock-unit-gbs",
        "overclock-unit-mbs",
    ];

    private static string ReadRepoFile(params string[] parts)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CA-O.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("CA-O.sln not found above " + AppContext.BaseDirectory);
    }

    private static string[] OverclockKnobIds() =>
        OverclockCatalog.NvidiaGpuKnobs
            .Concat(OverclockCatalog.CpuPolicyKnobs)
            .Concat(OverclockCatalog.MemoryKnobs)
            .Select(k => k.Id.Value)
            .ToArray();

    private static OverclockViewModel ViewModel(IOverclockDeltaSource source, IOverclockDeltaStore? store = null) =>
        new(
            new OverclockReadOnlyRegistry(),
            [],
            new GameDetectionProvider(),
            source,
            store ?? new InMemoryDeltaStore());

    private static async Task Measure(OverclockViewModel viewModel)
    {
        await viewModel.CaptureBaselineCommand.ExecuteAsync(null);
        await viewModel.CaptureCandidateCommand.ExecuteAsync(null);
    }

    private static DeltaSample Sample(
        double cpu,
        double memory = 500,
        double read = 500,
        double write = 500) =>
        new(
            At,
            [
                new AxisMeasurement(DeltaAxis.Cpu, cpu, "overclock-unit-score", EvidenceLevel.Benchmark),
                new AxisMeasurement(DeltaAxis.Memory, memory, "overclock-unit-gbs", EvidenceLevel.Benchmark),
                new AxisMeasurement(DeltaAxis.StorageRead, read, "overclock-unit-mbs", EvidenceLevel.Benchmark),
                new AxisMeasurement(DeltaAxis.StorageWrite, write, "overclock-unit-mbs", EvidenceLevel.Benchmark),
            ]);

    private sealed class ScriptedDeltaSource(params DeltaSample[] script) : IOverclockDeltaSource
    {
        private int _next;

        public ValueTask<DeltaSample> CaptureAsync(CancellationToken ct) =>
            ValueTask.FromResult(script[Math.Min(_next++, script.Length - 1)]);
    }

    /// <summary>Proves the delta path is never reached: the source would blow up.</summary>
    private sealed class ThrowingDeltaSource : IOverclockDeltaSource
    {
        public ValueTask<DeltaSample> CaptureAsync(CancellationToken ct) =>
            throw new InvalidOperationException("no measurement was asked for");
    }

    private sealed class InMemoryDeltaStore : IOverclockDeltaStore
    {
        public List<OverclockDeltaReport> Saved { get; } = [];

        public Task SaveAsync(OverclockDeltaReport report, CancellationToken ct)
        {
            Saved.Add(report);
            return Task.CompletedTask;
        }

        public Task<OverclockDeltaReport?> LoadAsync(CancellationToken ct) =>
            Task.FromResult(Saved.Count == 0 ? null : Saved[^1]);
    }
}