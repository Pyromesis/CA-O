using System.Globalization;
using CAO.Shared;

namespace CAO.Core.Overclock;

/// <summary>Stable identifier of a knob, e.g. "nvidia.gpu.powerLimit".</summary>
public sealed record KnobId
{
    public KnobId(string value) => Value = value;

    public string Value { get; }

    public static implicit operator KnobId(string value) => new(value);

    public override string ToString() => Value;
}

/// <summary>A value a knob can hold. Scalars carry the double; enums the index.</summary>
public readonly record struct KnobValue
{
    private KnobValue(double scalar, int index)
    {
        Scalar = scalar;
        Index = index;
    }

    public double Scalar { get; }

    public int Index { get; }

    public static KnobValue FromScalar(double value) => new(value, 0);

    public static KnobValue FromIndex(int index) => new(0d, index);

    /// <summary>Scalar values are the common case in the catalog, so literals convert implicitly.</summary>
    public static implicit operator KnobValue(double value) => FromScalar(value);

    /// <summary>Discrete values index into <see cref="KnobRange.Allowed"/>.</summary>
    public static implicit operator KnobValue(int index) => FromIndex(index);

    public override string ToString() =>
        Index == 0
            ? Scalar.ToString("0.##", CultureInfo.InvariantCulture)
            : Index.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The accepted value space of a knob. For <see cref="KnobKind.Enum"/> the
/// discrete set is carried explicitly; for scalars it is Min/Max/Step.
/// </summary>
public sealed record KnobRange
{
    private KnobRange(double min, double max, IReadOnlyList<KnobValue>? allowed)
    {
        Min = min;
        Max = max;
        Allowed = allowed;
    }

    public double Min { get; }

    public double Max { get; }

    public IReadOnlyList<KnobValue>? Allowed { get; }

    public static KnobRange Scalar(double min, double max) => new(min, max, null);

    public static KnobRange Discrete(IReadOnlyList<KnobValue> allowed) =>
        new(0d, Math.Max(0, allowed.Count - 1), allowed);

    public bool Accepts(KnobValue value)
    {
        if (Allowed is { Count: > 0 })
        {
            return Allowed.Any(a => a.Index == value.Index);
        }

        return value.Scalar >= Min && value.Scalar <= Max;
    }
}

/// <summary>A named starting point for a knob inside a tuning profile.</summary>
public sealed record KnobPreset(OverclockProfile Profile, KnobValue Value, string LabelKey);

/// <summary>
/// A constraint between two knobs. This is what keeps the declarative catalog
/// honest: a clock offset without its voltage partner is dead performance plus
/// extra risk, and the engine refuses it instead of letting the user find out.
/// </summary>
public sealed record KnobDependency(KnobId Other, DependencyKind Kind);

/// <summary>
/// One tunable in the catalog. Every knob the app can expose is data, so the
/// UI, the validator and the tuning engine all derive from this same record.
/// </summary>
public sealed record OverclockKnob(
    KnobId Id,
    OverclockComponent Component,
    KnobKind Kind,
    KnobRange Range,
    double Step,
    KnobUnit Unit,
    RiskLevel Risk,
    EvidenceLevel Evidence,
    string LabelKey,
    string DescriptionKey,
    bool RequiresReboot,
    KnobScope Scope,
    string BackendId,
    IReadOnlyList<KnobPreset> Presets,
    IReadOnlyList<KnobDependency> DependsOn)
{
    public KnobValue? PresetValue(OverclockProfile profile) =>
        Presets.FirstOrDefault(p => p.Profile == profile)?.Value;
}

/// <summary>Availability of one backend on this machine, with the real reason.</summary>
public sealed record BackendAvailability(
    string BackendId,
    bool Available,
    string UnavailableReasonKey,
    string? RemedyHintKey,
    bool RequiresRestart,
    string? DriverVersion,
    EvidenceLevel LastProbeEvidence);

/// <summary>The result of probing this specific machine with this specific driver.</summary>
public sealed record OverclockCapabilities(
    CpuBackendKind Cpu,
    GpuBackendKind Gpu,
    MemoryBackendKind Memory,
    IReadOnlyList<BackendAvailability> Details,
    IReadOnlyList<OverclockKnob> Knobs)
{
    public static OverclockCapabilities Empty { get; } = new(
        CpuBackendKind.None,
        GpuBackendKind.None,
        MemoryBackendKind.None,
        Array.Empty<BackendAvailability>(),
        Array.Empty<OverclockKnob>());
}