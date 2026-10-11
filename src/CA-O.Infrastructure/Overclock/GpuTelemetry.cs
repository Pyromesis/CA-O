using CAO.Core.Overclock;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// One measured fact shown in the UI. The backend does not localize: it returns
/// a key and the already-formatted value, because only the UI knows the user's
/// language. Same discipline as <see cref="OverclockKnob.LabelKey"/>.
/// </summary>
/// <param name="LabelKey">Localization key for the reading's name.</param>
/// <param name="Value">Already formatted, already truthful. Never a guess.</param>
/// <param name="Evidence">Grading of how this number was obtained.</param>
/// <param name="UnavailableReasonKey">
/// Set when the sensor could not be read at all. The UI then shows the reason
/// instead of a fabricated zero (design 6.12).
/// </param>
public sealed record OverclockReading(
    string LabelKey,
    string Value,
    EvidenceLevel Evidence,
    string? UnavailableReasonKey = null);

/// <summary>
/// Optional capability a backend exposes when it can report values that are not
/// catalog knobs (raw clocks, fan duty, driver token). The UI asks for it
/// through <see cref="IOverclockBackendRegistry"/> so it never needs a concrete
/// backend type.
/// </summary>
public interface IOverclockDetailSource
{
    ValueTask<IReadOnlyList<OverclockReading>> ReadDetailsAsync(CancellationToken ct);
}

/// <summary>
/// Whether a backend can actually write right now. F1 is read-only for GPU
/// silicon, so the gate answers honestly "no" with the reason the UI shows.
/// F3 flips it without touching a single UI file.
/// </summary>
public interface IOverclockWriteGate
{
    bool CanWrite(KnobId knob);

    string? BlockReasonKey(KnobId knob);
}

/// <summary>Catalog ids for the NVIDIA knob set.</summary>
public static class NvKnobs
{
    public static readonly KnobId PowerLimit = "nvidia.gpu.powerLimit";

    public static readonly KnobId ThermalLimit = "nvidia.gpu.thermalLimit";

    public static readonly KnobId CoreClockOffset = "nvidia.gpu.coreClockOffset";

    public static readonly KnobId MemoryClockOffset = "nvidia.gpu.memoryClockOffset";

    public static readonly KnobId FanTarget = "nvidia.gpu.fanTarget";

    public static readonly KnobId ClockReadout = "nvidia.gpu.clockReadout";
}