using CAO.Core.Overclock;
using CAO.Core.Overclock.Reporting;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// Reads the bounds NVIDIA's driver reports for the settings CA-O can write.
///
/// The catalog models the power limit as a percentage and the clock offsets in
/// hertz, because those are the units a person reasons in ("120 % of stock").
/// NVML answers in milliwatts and megahertz. This class is the only place that
/// conversion lives, and it uses the same anchor the writer uses - the default
/// power limit, falling back to the minimum - so the ceiling shown to the user
/// and the value the driver was handed describe the same scale. A ceiling in a
/// different scale than the knob would be a number that reads plausible and
/// means nothing.
///
/// Only writable knobs get a row. A ceiling for a setting that cannot be
/// written is noise: the knob row already says there is no public setter.
/// </summary>
public sealed class NvidiaTuningCeilingSource : IOverclockCeilingSource
{
    /// <summary>Localization key for "the driver did not report this bound".</summary>
    public const string UnavailableReasonKey = "overclock-ceiling-unavailable";

    /// <summary>Localization key for "there is no anchor to express a percentage against".</summary>
    public const string AnchorUnavailableReasonKey = "overclock-ceiling-anchor-unavailable";

    /// <summary>
    /// Localization key for "the driver enforces less than the card advertises".
    /// Worth saying out loud: it is the difference between a ceiling the user
    /// can push against and one that is already being enforced for them.
    /// </summary>
    public const string EnforcedBelowMaxReasonKey = "overclock-ceiling-enforced-below-max";

    /// <summary>Localization key for the power limit unit.</summary>
    public const string PercentUnitKey = "overclock-unit-percent";

    /// <summary>Localization key for the clock unit.</summary>
    public const string HertzUnitKey = "overclock-unit-hertz";

    private const double HertzPerMegahertz = 1_000_000d;

    private readonly INvidiaTuningApi _tuning;

    public NvidiaTuningCeilingSource(INvidiaTuningApi tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<TuningCeiling>> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // No write path, nothing to bound. The knob row already carries the
        // dependency reason; repeating it here would be a second copy of one
        // warning that reads as if it came from a different place.
        if (!_tuning.IsAvailable)
        {
            return new ValueTask<IReadOnlyList<TuningCeiling>>(Array.Empty<TuningCeiling>());
        }

        if (!_tuning.TryDescribe(out var device) || device is null)
        {
            return new ValueTask<IReadOnlyList<TuningCeiling>>(
                [
                    Unknown(NvKnobs.PowerLimit, PercentUnitKey),
                    Unknown(NvKnobs.CoreClockOffset, HertzUnitKey),
                    Unknown(NvKnobs.MemoryClockOffset, HertzUnitKey),
                ]);
        }

        IReadOnlyList<TuningCeiling> ceilings =
        [
            PowerCeiling(device),
            OffsetCeiling(NvKnobs.CoreClockOffset, device.CoreOffsetMinMegahertz, device.CoreOffsetMaxMegahertz),
            OffsetCeiling(NvKnobs.MemoryClockOffset, device.MemoryOffsetMinMegahertz, device.MemoryOffsetMaxMegahertz),
        ];

        return new ValueTask<IReadOnlyList<TuningCeiling>>(ceilings);
    }

    private static TuningCeiling Unknown(KnobId knob, string unit) =>
        new(knob, unit, null, null, UnavailableReasonKey);

    private static TuningCeiling OffsetCeiling(KnobId knob, int? minMegahertz, int? maxMegahertz) =>
        minMegahertz is null || maxMegahertz is null
            ? Unknown(knob, HertzUnitKey)
            : new TuningCeiling(
                knob,
                HertzUnitKey,
                minMegahertz.Value * HertzPerMegahertz,
                maxMegahertz.Value * HertzPerMegahertz,
                null);

    private static TuningCeiling PowerCeiling(NvidiaTuningDevice device)
    {
        var anchor = device.DefaultPowerLimitMilliwatts ?? device.MinPowerLimitMilliwatts;
        if (anchor is null or <= 0)
        {
            return new TuningCeiling(NvKnobs.PowerLimit, PercentUnitKey, null, null, AnchorUnavailableReasonKey);
        }

        if (device.MinPowerLimitMilliwatts is null || device.MaxPowerLimitMilliwatts is null)
        {
            return Unknown(NvKnobs.PowerLimit, PercentUnitKey);
        }

        var enforced = device.EnforcedPowerLimitMilliwatts;
        var max = device.MaxPowerLimitMilliwatts.Value;
        var reason = enforced is not null && enforced.Value < max ? EnforcedBelowMaxReasonKey : null;

        return new TuningCeiling(
            NvKnobs.PowerLimit,
            PercentUnitKey,
            ToPercent(device.MinPowerLimitMilliwatts.Value, anchor.Value),
            ToPercent(max, anchor.Value),
            reason);
    }

    private static double ToPercent(int milliwatts, int anchorMilliwatts) =>
        Math.Round(milliwatts * 100d / anchorMilliwatts, 2, MidpointRounding.AwayFromZero);
}