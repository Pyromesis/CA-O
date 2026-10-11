using CAO.Core.Benchmark;
using CAO.Shared;

namespace CAO.Core.Overclock.Reporting;

/// <summary>
/// The axes an overclock profile can move. Every axis is "higher is better"
/// (a score, a bandwidth, a throughput), on purpose: an axis that improves by
/// going down would force every reader of a delta to know the sign per axis,
/// and whoever gets that backwards reports a slowdown as a win.
/// </summary>
public enum DeltaAxis
{
    Cpu,
    Memory,
    StorageRead,
    StorageWrite,
}

/// <summary>Verdict for one axis, or for the profile as a whole.</summary>
public enum DeltaVerdict
{
    /// <summary>One of the two samples had nothing to compare. Never "neutral".</summary>
    NoData,

    /// <summary>Beyond the noise floor, in the direction the profile aims for.</summary>
    Improvement,

    /// <summary>Inside the noise floor: the profile did not measurably move it.</summary>
    NoMeasurableChange,

    /// <summary>Beyond the noise floor, in the wrong direction.</summary>
    Regression,
}

/// <summary>One measured number, with the unit it was measured in.</summary>
public sealed record AxisMeasurement(DeltaAxis Axis, double Value, string Unit, EvidenceLevel Evidence);

/// <summary>
/// One complete measurement pass: the axes it managed to read, and when.
/// A sample carries axis values and nothing else. That is the structural
/// reason this file can promise the report never attributes a delta to an
/// individual knob: there is no field here where a knob could travel.
/// </summary>
public sealed record DeltaSample(DateTimeOffset TakenAtUtc, IReadOnlyList<AxisMeasurement> Axes)
{
    public AxisMeasurement? Axis(DeltaAxis axis) => Axes.FirstOrDefault(m => m.Axis == axis);
}

/// <summary>
/// The ceiling that stopped an axis from going further: the thermal limit, the
/// driver constraint, or the stability ceiling the tuner found by bisection.
/// A null <see cref="Value"/> means "no ceiling known", which is not the same
/// thing as "no ceiling".
/// </summary>
public sealed record DeltaCeiling(DeltaAxis Axis, double? Value, string Unit, string ReasonKey);

/// <summary>One axis of the before/after comparison.</summary>
public sealed record DeltaAxisRow(
    DeltaAxis Axis,
    double? Baseline,
    double? Candidate,
    double? DeltaPercent,
    DeltaVerdict Verdict,
    string Unit,
    EvidenceLevel Evidence,
    string? ReasonKey);

/// <summary>
/// What the profile actually bought. Note what is missing: there is no knob
/// anywhere in this type. A profile is applied as a package and its effect is
/// measured as a package; overclocking is not additive (more power raises the
/// memory clock ceiling but eats thermal headroom), so splitting the delta
/// across knobs would be arithmetic fiction dressed up as a measurement.
/// </summary>
public sealed record OverclockDeltaReport(
    DateTimeOffset MeasuredAtUtc,
    DeltaVerdict Verdict,
    double NoiseFloorPercent,
    IReadOnlyList<DeltaAxisRow> Axes,
    IReadOnlyList<DeltaCeiling> Ceilings)
{
    /// <summary>False when every axis came back NoData: the UI shows "no data", never 0%.</summary>
    public bool HasData => Axes.Any(r => r.Verdict is not DeltaVerdict.NoData);

    public static OverclockDeltaReport Empty { get; } = new(
        DateTimeOffset.UnixEpoch,
        DeltaVerdict.NoData,
        BenchmarkPolicy.MinimumEffectPercent,
        [],
        []);
}

/// <summary>Turns two samples into one honest report. Pure: no I/O, no clock.</summary>
public static class OverclockDeltaBuilder
{
    /// <summary>The axis was missing from at least one of the two samples.</summary>
    public const string NoDataReasonKey = "overclock-delta-no-data";

    /// <summary>The baseline was zero or negative, so a percentage is undefined.</summary>
    public const string ZeroBaselineReasonKey = "overclock-delta-zero-baseline";

    /// <summary>The two samples were measured in different units; the ratio would be meaningless.</summary>
    public const string UnitMismatchReasonKey = "overclock-delta-unit-mismatch";

    /// <summary>
    /// Compares baseline against candidate. Every declared axis gets a row,
    /// including the ones neither sample read: an axis that silently vanishes
    /// reads as "no change", and "we did not measure it" has to look different
    /// from "we measured it and it did not move".
    /// </summary>
    public static OverclockDeltaReport Build(
        DeltaSample baseline,
        DeltaSample candidate,
        IReadOnlyList<DeltaCeiling>? ceilings = null,
        double noiseFloorPercent = BenchmarkPolicy.MinimumEffectPercent)
    {
        var rows = new List<DeltaAxisRow>();
        foreach (var axis in Enum.GetValues<DeltaAxis>())
        {
            rows.Add(Compare(axis, baseline, candidate, noiseFloorPercent));
        }

        return new OverclockDeltaReport(
            candidate.TakenAtUtc,
            OverallVerdict(rows),
            noiseFloorPercent,
            rows,
            ceilings ?? []);
    }

    private static DeltaAxisRow Compare(
        DeltaAxis axis,
        DeltaSample baseline,
        DeltaSample candidate,
        double noiseFloorPercent)
    {
        var before = baseline.Axis(axis);
        var after = candidate.Axis(axis);

        if (before is null || after is null)
        {
            return Unread(axis, before, after, NoDataReasonKey);
        }

        if (!string.Equals(before.Unit, after.Unit, StringComparison.Ordinal))
        {
            return Unread(axis, before, after, UnitMismatchReasonKey);
        }

        if (before.Value <= 0d)
        {
            return Unread(axis, before, after, ZeroBaselineReasonKey);
        }

        var delta = (after.Value - before.Value) / before.Value * 100d;
        var verdict = DeltaVerdict.NoMeasurableChange;
        if (delta >= noiseFloorPercent)
        {
            verdict = DeltaVerdict.Improvement;
        }
        else if (delta <= -noiseFloorPercent)
        {
            verdict = DeltaVerdict.Regression;
        }

        return new DeltaAxisRow(
            axis,
            before.Value,
            after.Value,
            delta,
            verdict,
            after.Unit,
            Weaker(before.Evidence, after.Evidence),
            null);
    }

    private static DeltaAxisRow Unread(
        DeltaAxis axis,
        AxisMeasurement? before,
        AxisMeasurement? after,
        string reasonKey) =>
        new(
            axis,
            before?.Value,
            after?.Value,
            null,
            DeltaVerdict.NoData,
            after?.Unit ?? before?.Unit ?? string.Empty,
            Weaker(
                before?.Evidence ?? EvidenceLevel.Unknown,
                after?.Evidence ?? EvidenceLevel.Unknown),
            reasonKey);

    private static DeltaVerdict OverallVerdict(IReadOnlyList<DeltaAxisRow> rows)
    {
        var measured = rows.Where(r => r.Verdict is not DeltaVerdict.NoData).ToList();
        if (measured.Count == 0)
        {
            return DeltaVerdict.NoData;
        }

        // A regression anywhere vetoes an improvement. The profile ships as a
        // package, so the axis that got worse ships with it, and a headline
        // "faster" that hides a slower axis is the exact dishonesty this
        // section exists to replace.
        if (measured.Exists(r => r.Verdict is DeltaVerdict.Regression))
        {
            return DeltaVerdict.Regression;
        }

        return measured.Exists(r => r.Verdict is DeltaVerdict.Improvement)
            ? DeltaVerdict.Improvement
            : DeltaVerdict.NoMeasurableChange;
    }

    /// <summary>
    /// The weaker of the two evidences: a delta is only ever as trustworthy as
    /// the least trustworthy number inside it, so an Official baseline cannot
    /// lend its authority to a Heuristic candidate.
    /// </summary>
    private static EvidenceLevel Weaker(EvidenceLevel a, EvidenceLevel b) => Rank(a) >= Rank(b) ? a : b;

    private static int Rank(EvidenceLevel level) => level switch
    {
        EvidenceLevel.Official => 0,
        EvidenceLevel.Vendor => 1,
        EvidenceLevel.Benchmark => 2,
        EvidenceLevel.Empirical => 3,
        EvidenceLevel.Heuristic => 4,
        _ => 5,
    };
}