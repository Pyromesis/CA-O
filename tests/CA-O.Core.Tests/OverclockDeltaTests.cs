using System.Reflection;
using CAO.Core.Benchmark;
using CAO.Core.Overclock;
using CAO.Core.Overclock.Reporting;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// The delta report is the one place where CA-O could easily start lying:
/// a percentage with no denominator, a regression averaged away, or a knob
/// credited with a share of a package that moved as a whole. These tests hold
/// the report to that.
/// </summary>
public sealed class OverclockDeltaTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_ProducesOneRowPerDeclaredAxis()
    {
        var report = OverclockDeltaBuilder.Build(Sample(100), Sample(110));

        Assert.Equal(Enum.GetValues<DeltaAxis>().Length, report.Axes.Count);
        Assert.Equal(
            Enum.GetValues<DeltaAxis>().OrderBy(a => a),
            report.Axes.Select(r => r.Axis).OrderBy(a => a));
    }

    [Fact]
    public void Build_UsesTheSharedNoiseFloor()
    {
        var report = OverclockDeltaBuilder.Build(Sample(100), Sample(100));

        Assert.Equal(BenchmarkPolicy.MinimumEffectPercent, report.NoiseFloorPercent);
    }

    [Fact]
    public void Build_ReportsImprovement_OnlyBeyondTheNoiseFloor()
    {
        var justUnder = OverclockDeltaBuilder.Build(Sample(100), Sample(100 + (BenchmarkPolicy.MinimumEffectPercent - 0.1)));
        var justOver = OverclockDeltaBuilder.Build(Sample(100), Sample(100 + BenchmarkPolicy.MinimumEffectPercent));

        Assert.Equal(DeltaVerdict.NoMeasurableChange, Row(justUnder, DeltaAxis.Cpu).Verdict);
        Assert.Equal(DeltaVerdict.Improvement, Row(justOver, DeltaAxis.Cpu).Verdict);
    }

    [Fact]
    public void Build_ReportsARegression_WhenTheAxisLostGround()
    {
        var report = OverclockDeltaBuilder.Build(Sample(100), Sample(100 - (BenchmarkPolicy.MinimumEffectPercent + 1)));

        var row = Row(report, DeltaAxis.Cpu);
        Assert.Equal(DeltaVerdict.Regression, row.Verdict);
        Assert.True(row.DeltaPercent < 0d);
    }

    [Fact]
    public void Build_LetsARegressionVetoAnImprovement()
    {
        // CPU up 20%, disk read down 20%: shipping the profile ships both.
        var report = OverclockDeltaBuilder.Build(Sample(100, 100, 100, 100), Sample(120, 100, 80, 100));

        Assert.Equal(DeltaVerdict.Regression, report.Verdict);
    }

    [Fact]
    public void Build_CallsItImprovement_WhenAnyAxisMovedAndNoneRegressed()
    {
        var report = OverclockDeltaBuilder.Build(Sample(100, 100, 100, 100), Sample(120, 101, 99, 100));

        Assert.Equal(DeltaVerdict.Improvement, report.Verdict);
        Assert.Equal(DeltaVerdict.Improvement, Row(report, DeltaAxis.Cpu).Verdict);
        Assert.Equal(DeltaVerdict.NoMeasurableChange, Row(report, DeltaAxis.Memory).Verdict);
    }

    [Fact]
    public void Build_CallsItNoMeasurableChange_WhenEverythingStayedInsideTheFloor()
    {
        var report = OverclockDeltaBuilder.Build(Sample(100), Sample(101));

        Assert.Equal(DeltaVerdict.NoMeasurableChange, report.Verdict);
        Assert.True(report.HasData);
    }

    [Fact]
    public void Build_SaysNoData_WhenAnAxisIsMissingFromOneSample()
    {
        var baseline = new DeltaSample(At, [new AxisMeasurement(DeltaAxis.Cpu, 100, "u", EvidenceLevel.Benchmark)]);
        var candidate = new DeltaSample(At, []);

        var row = Row(OverclockDeltaBuilder.Build(baseline, candidate), DeltaAxis.Cpu);

        Assert.Equal(DeltaVerdict.NoData, row.Verdict);
        Assert.Equal(OverclockDeltaBuilder.NoDataReasonKey, row.ReasonKey);
        Assert.Null(row.DeltaPercent);
    }

    [Fact]
    public void Build_KeepsAnUnreadAxisVisible_InsteadOfDroppingIt()
    {
        var empty = new DeltaSample(At, []);
        var report = OverclockDeltaBuilder.Build(empty, empty);

        // An axis that silently disappears reads as "no change"; "we did not
        // measure it" has to look different, so the row stays with a reason.
        Assert.All(report.Axes, row =>
        {
            Assert.Equal(DeltaVerdict.NoData, row.Verdict);
            Assert.False(string.IsNullOrEmpty(row.ReasonKey));
        });
        Assert.False(report.HasData);
        Assert.Equal(DeltaVerdict.NoData, report.Verdict);
    }

    [Fact]
    public void Build_RefusesAPercentage_OnAZeroBaseline()
    {
        var baseline = new DeltaSample(At, [new AxisMeasurement(DeltaAxis.Cpu, 0, "u", EvidenceLevel.Benchmark)]);
        var candidate = new DeltaSample(At, [new AxisMeasurement(DeltaAxis.Cpu, 50, "u", EvidenceLevel.Benchmark)]);

        var row = Row(OverclockDeltaBuilder.Build(baseline, candidate), DeltaAxis.Cpu);

        Assert.Equal(DeltaVerdict.NoData, row.Verdict);
        Assert.Equal(OverclockDeltaBuilder.ZeroBaselineReasonKey, row.ReasonKey);
        Assert.Null(row.DeltaPercent);
        Assert.Equal(0d, row.Baseline);
        Assert.Equal(50d, row.Candidate);
    }

    [Fact]
    public void Build_RefusesToCompareSamplesMeasuredInDifferentUnits()
    {
        var baseline = new DeltaSample(At, [new AxisMeasurement(DeltaAxis.Cpu, 100, "score", EvidenceLevel.Benchmark)]);
        var candidate = new DeltaSample(At, [new AxisMeasurement(DeltaAxis.Cpu, 900, "MHz", EvidenceLevel.Benchmark)]);

        var row = Row(OverclockDeltaBuilder.Build(baseline, candidate), DeltaAxis.Cpu);

        Assert.Equal(DeltaVerdict.NoData, row.Verdict);
        Assert.Equal(OverclockDeltaBuilder.UnitMismatchReasonKey, row.ReasonKey);
        Assert.Null(row.DeltaPercent);
    }

    [Fact]
    public void Build_GradesTheRowWithTheWeakerOfTheTwoEvidences()
    {
        var baseline = new DeltaSample(At, [new AxisMeasurement(DeltaAxis.Cpu, 100, "u", EvidenceLevel.Official)]);
        var candidate = new DeltaSample(At, [new AxisMeasurement(DeltaAxis.Cpu, 120, "u", EvidenceLevel.Heuristic)]);

        Assert.Equal(EvidenceLevel.Heuristic, Row(OverclockDeltaBuilder.Build(baseline, candidate), DeltaAxis.Cpu).Evidence);
    }

    [Fact]
    public void Build_KeepsAnUnreadAxisEvidenceUnknown()
    {
        var empty = new DeltaSample(At, []);
        var row = Row(OverclockDeltaBuilder.Build(empty, empty), DeltaAxis.Cpu);

        Assert.Equal(EvidenceLevel.Unknown, row.Evidence);
    }

    [Fact]
    public void Build_CarriesTheCeilingsThrough_Untouched()
    {
        var ceilings = new[]
        {
            new TuningCeiling(new KnobId("nvidia.gpu.coreClockOffset"), "MHz", -500d, 500d, null),
        };

        var report = OverclockDeltaBuilder.Build(Sample(100), Sample(110), ceilings);

        Assert.Equal(ceilings, report.Ceilings);
    }

    [Fact]
    public void Build_AcceptsNoCeilingsAtAll()
    {
        Assert.Empty(OverclockDeltaBuilder.Build(Sample(100), Sample(110)).Ceilings);
    }

    [Fact]
    public void Empty_HasNoDataAndIsNotImprovement()
    {
        Assert.False(OverclockDeltaReport.Empty.HasData);
        Assert.Equal(DeltaVerdict.NoData, OverclockDeltaReport.Empty.Verdict);
    }

    [Theory]
    [MemberData(nameof(DeltaTypes))]
    public void Delta_NeverCarriesAKnob(Type deltaType)
    {
        // Overclocking is not additive: more power lifts the memory clock
        // ceiling while eating thermal headroom, so a delta cannot be split
        // across knobs. The guarantee is structural rather than a promise,
        // because no field exists where a knob could travel.
        var offenders = deltaType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => typeof(KnobId).IsAssignableFrom(p.PropertyType))
            .Select(p => p.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    public static TheoryData<Type> DeltaTypes() =>
    [
        typeof(DeltaAxisRow),
        typeof(DeltaSample),
        typeof(AxisMeasurement),
    ];

    [Fact]
    public void Ceiling_NamesTheKnobItBounded()
    {
        // The other half of the same rule. A ceiling is a fact about one
        // setting ("this one stopped here"), not a share of the result, so
        // this is the one place in the report allowed to name a knob.
        var knob = new KnobId("nvidia.gpu.coreClockOffset");

        var ceiling = new TuningCeiling(knob, "MHz", -500d, 500d, null);

        Assert.Equal(knob, ceiling.Knob);
        Assert.Equal(-500d, ceiling.Min);
        Assert.Equal(500d, ceiling.Max);
        Assert.Null(ceiling.ReasonKey);
    }

    private static DeltaAxisRow Row(OverclockDeltaReport report, DeltaAxis axis) =>
        report.Axes.Single(r => r.Axis == axis);

    private static DeltaSample Sample(
        double cpu,
        double memory = 100,
        double read = 100,
        double write = 100,
        EvidenceLevel evidence = EvidenceLevel.Benchmark) =>
        new(
            At,
            [
                new AxisMeasurement(DeltaAxis.Cpu, cpu, "u", evidence),
                new AxisMeasurement(DeltaAxis.Memory, memory, "u", evidence),
                new AxisMeasurement(DeltaAxis.StorageRead, read, "u", evidence),
                new AxisMeasurement(DeltaAxis.StorageWrite, write, "u", evidence),
            ]);
}