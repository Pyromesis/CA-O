using CAO.Core.Overclock;
using CAO.Core.Overclock.Reporting;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// The real bounds ADLX reports for the one AMD knob with a public setter.
/// AMD power limits already are percent of TDP, so there is no anchor to
/// resolve and no conversion to get wrong: the driver range is the ceiling.
/// A range the driver would not report gets a row with its reason, not a
/// missing row that would read as "nothing bounded it".
/// </summary>
public sealed class AmdTuningCeilingSource : IOverclockCeilingSource
{
    private readonly IAmdTuningApi _tuning;

    public AmdTuningCeilingSource(IAmdTuningApi tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    public ValueTask<IReadOnlyList<TuningCeiling>> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_tuning.IsAvailable)
        {
            return new ValueTask<IReadOnlyList<TuningCeiling>>([]);
        }

        if (!_tuning.TryDescribe(out var device) || device is null)
        {
            TuningCeiling unknown = new(
                AmdKnobs.PowerLimit,
                NvidiaTuningCeilingSource.PercentUnitKey,
                null,
                null,
                _tuning.UnavailableReasonKey ?? NvidiaTuningCeilingSource.UnavailableReasonKey);
            return new ValueTask<IReadOnlyList<TuningCeiling>>([unknown]);
        }

        if (device.MinPowerLimitPercent is not int min || device.MaxPowerLimitPercent is not int max)
        {
            TuningCeiling noRange = new(
                AmdKnobs.PowerLimit,
                NvidiaTuningCeilingSource.PercentUnitKey,
                null,
                null,
                AmdAdlxBackend.RangeUnavailableReasonKey);
            return new ValueTask<IReadOnlyList<TuningCeiling>>([noRange]);
        }

        // Same scale as the knob: % of stock, whatever ADLX's own unit is.
        TuningCeiling ceiling = new(
            AmdKnobs.PowerLimit,
            NvidiaTuningCeilingSource.PercentUnitKey,
            AmdAdlxBackend.ToCatalogPercent(min, device),
            AmdAdlxBackend.ToCatalogPercent(max, device),
            null);
        return new ValueTask<IReadOnlyList<TuningCeiling>>([ceiling]);
    }
}
