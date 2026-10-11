using System.Management;
using System.Runtime.InteropServices;
using CAO.Core.Overclock.Stability;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// Measures effective against advertised CPU clocks through WMI. A sustained gap
/// means the silicon is throttling or the policy is lying, and either way the
/// tuning engine must hear about it before pushing further.
///
/// What it does NOT do is equally deliberate: WHEA errors and kernel bugchecks
/// live in the Windows Event Log, which this repo refuses to take a NuGet for,
/// so those metrics are reported as <c>SensorUnavailable</c> (never a fault,
/// never a pass) instead of being guessed at. Absence of telemetry is a fact,
/// not a verdict.
/// </summary>
public sealed class WmiClockStallProbe : IStabilityProbe
{
    public const string UnavailableMetricReasonKey = "overclock-probe-metric-unavailable";

    private const int MaxSamples = 120;

    /// <summary>
    /// Below this average load a clock gap is downclocking, not instability:
    /// an idle core at half speed is the power plan working, and grading it a
    /// fault would make every tuning run find its "ceiling" on the first step.
    /// </summary>
    private const int IdleLoadPercentCutoff = 50;

    public string Id => "wmi-clock-stall";

    public IReadOnlyList<ProbeMetric> SupportedMetrics => [ProbeMetric.EffectiveClockStall];

    public async Task<ProbeRun> RunAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unsupported = request.Metrics
            .Where(m => m != ProbeMetric.EffectiveClockStall)
            .Select(m => (ProbeEvidence)new ProbeEvidence.SensorUnavailable(m.ToString(), UnavailableMetricReasonKey))
            .ToList();

        var count = Math.Clamp((int)request.Duration.TotalSeconds, 1, MaxSamples);
        var samples = new List<ProbeSample>(count);

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clocks = await Task.Run(ReadClocks, cancellationToken).ConfigureAwait(false);
            if (clocks is null)
            {
                // WMI stopped answering mid-run: the window did not run, so the
                // verdict is Pending, never a pass over missing data.
                return new ProbeRun(Completed: false, Aborted: false, samples, null);
            }

            var evidence = new List<ProbeEvidence>(unsupported.Count + 1)
            {
                clocks.Value.LoadPercent < IdleLoadPercentCutoff
                    ? new ProbeEvidence.ClockStall(clocks.Value.MaxMhz, clocks.Value.MaxMhz, 0)
                    : new ProbeEvidence.ClockStall(
                        clocks.Value.MaxMhz,
                        clocks.Value.CurrentMhz,
                        clocks.Value.MaxMhz <= 0
                            ? 0
                            : (clocks.Value.MaxMhz - clocks.Value.CurrentMhz) / clocks.Value.MaxMhz),
            };
            evidence.AddRange(unsupported);
            samples.Add(new ProbeSample(DateTimeOffset.UtcNow, evidence));

            if (i < count - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }

        return new ProbeRun(Completed: true, Aborted: false, samples, null);
    }

    private static (double CurrentMhz, double MaxMhz, double LoadPercent)? ReadClocks()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentClockSpeed, MaxClockSpeed, LoadPercentage FROM Win32_Processor");

            double current = 0;
            double max = 0;
            double load = 0;
            var seen = 0;
            foreach (var obj in searcher.Get())
            {
                using (obj)
                {
                    if (TryMHz(obj["CurrentClockSpeed"], out var cur)
                        && TryMHz(obj["MaxClockSpeed"], out var top) && top > 0)
                    {
                        current += cur;
                        max += top;
                        load += TryPercent(obj["LoadPercentage"], out var pct) ? pct : 0;
                        seen++;
                    }
                }
            }

            return seen == 0 ? null : (current / seen, max / seen, load / seen);
        }
        catch (ManagementException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static bool TryPercent(object? raw, out double percent)
    {
        percent = 0;
        if (raw is null)
        {
            return false;
        }

        if (!double.TryParse(
            raw.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed) || parsed < 0 || parsed > 100)
        {
            return false;
        }

        percent = parsed;
        return true;
    }

    private static bool TryMHz(object? raw, out double mhz)
    {
        mhz = 0;
        if (raw is null)
        {
            return false;
        }

        if (!double.TryParse(
            raw.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed) || parsed <= 0)
        {
            return false;
        }

        mhz = parsed;
        return true;
    }
}
