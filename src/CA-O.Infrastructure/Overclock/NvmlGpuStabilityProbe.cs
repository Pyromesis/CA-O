using CAO.Core.Overclock.Stability;

namespace CAO.Infrastructure.Overclock;

/// <summary>The NVML reads the GPU stability probe needs, behind a seam so it is testable without a card.</summary>
internal interface IGpuStabilityTelemetry
{
    bool TryResolve();

    bool TryGetGraphicsClock(out uint megahertz);

    bool TryGetUtilization(out uint percent);

    bool TryGetTemperature(out uint celsius);

    bool TryGetSlowdownTemperature(out uint celsius);
}

/// <summary>The real telemetry: the same single NVIDIA device the backend tunes.</summary>
internal sealed class NvmlGpuStabilityTelemetry : IGpuStabilityTelemetry
{
    private IntPtr _handle;

    public bool TryResolve() => NvmlInterop.TryResolveHandle(out _handle);

    public bool TryGetGraphicsClock(out uint megahertz) =>
        NvmlInterop.TryGetClockInfo(_handle, NvidiaClockDomain.Graphics, out megahertz);

    public bool TryGetUtilization(out uint percent) => NvmlInterop.TryGetUtilization(_handle, out percent);

    public bool TryGetTemperature(out uint celsius) => NvmlInterop.TryGetTemperature(_handle, out celsius);

    public bool TryGetSlowdownTemperature(out uint celsius) => NvmlInterop.TryGetSlowdownTemperature(_handle, out celsius);
}

/// <summary>
/// Stability probe for GPU clock offsets.
///
/// The tuning engine used to grade GPU offsets with <see cref="WmiClockStallProbe"/>,
/// which watches the CPU. With nothing loading the GPU that probe always passed,
/// so every offset step "validated" without the card ever running at the new
/// clock. This probe watches the GPU itself, once per second:
///  - the device stops answering (driver reset / TDR, the classic signature of
///    an unstable offset) → <see cref="ProbeEvidence.DeviceLost"/>, a fault;
///  - the die reaches the driver's slowdown temperature → thermal fault;
///  - the GPU was not actually loaded during the window → no verdict at all
///    (<see cref="StabilityVerdict.Pending"/>), never a pass. An offset tested
///    on an idle card proves nothing, so the user is told to run a game or a
///    benchmark while the tuning walks.
/// </summary>
public sealed class NvmlGpuStabilityProbe : IStabilityProbe
{
    /// <summary>Reason when the GPU could not be opened for telemetry.</summary>
    public const string UnavailableReasonKey = "overclock-probe-gpu-unavailable";

    /// <summary>Reason when the window ran without enough GPU load to mean anything.</summary>
    public const string NoLoadReasonKey = "overclock-probe-gpu-no-load";

    /// <summary>Reason carried by the device-lost evidence.</summary>
    public const string DeviceLostReasonKey = "overclock-probe-gpu-lost";

    /// <summary>Average GPU utilization below which a window is not a stability test.</summary>
    internal const double MinAverageLoadPercent = 70;

    private const int MaxSamples = 120;

    private readonly IGpuStabilityTelemetry _telemetry;
    private readonly TimeSpan _interval;

    public NvmlGpuStabilityProbe()
        : this(new NvmlGpuStabilityTelemetry(), TimeSpan.FromSeconds(1))
    {
    }

    internal NvmlGpuStabilityProbe(IGpuStabilityTelemetry telemetry, TimeSpan interval)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _interval = interval;
    }

    public string Id => "nvml-gpu-stability";

    public IReadOnlyList<ProbeMetric> SupportedMetrics =>
        [ProbeMetric.GpuDeviceLost, ProbeMetric.GpuThermalLimit];

    public async Task<ProbeRun> RunAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await Task.Run(_telemetry.TryResolve, cancellationToken).ConfigureAwait(false))
        {
            return ProbeRun.NotStarted(UnavailableReasonKey);
        }

        uint? slowdown = _telemetry.TryGetSlowdownTemperature(out uint limit) && limit > 0 ? limit : null;
        var count = Math.Clamp((int)request.Duration.TotalSeconds, 1, MaxSamples);
        var samples = new List<ProbeSample>(count);
        double loadSum = 0;
        var loadSamples = 0;

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var evidence = new List<ProbeEvidence>(2);

            if (!_telemetry.TryGetGraphicsClock(out _))
            {
                // The card answered when the window opened and does not now: the
                // driver reset under the new clock. That is a fault, not missing data.
                evidence.Add(new ProbeEvidence.DeviceLost("gpu", DeviceLostReasonKey));
                samples.Add(new ProbeSample(DateTimeOffset.UtcNow, evidence));
                return new ProbeRun(Completed: true, Aborted: false, samples);
            }

            if (_telemetry.TryGetUtilization(out uint load))
            {
                loadSum += load;
                loadSamples++;
            }

            if (_telemetry.TryGetTemperature(out uint celsius))
            {
                if (slowdown is uint threshold && celsius >= threshold)
                {
                    evidence.Add(new ProbeEvidence.ThermalLimitReached("gpu", threshold, celsius));
                }
            }
            else
            {
                evidence.Add(new ProbeEvidence.SensorUnavailable(nameof(ProbeMetric.GpuThermalLimit), WmiClockStallProbe.UnavailableMetricReasonKey));
            }

            samples.Add(new ProbeSample(DateTimeOffset.UtcNow, evidence));

            if (i < count - 1 && _interval > TimeSpan.Zero)
            {
                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
            }
        }

        var averageLoad = loadSamples == 0 ? 0 : loadSum / loadSamples;
        if (averageLoad < MinAverageLoadPercent)
        {
            // Not Completed: the evaluator turns this into Pending, and the engine
            // restores the last good value instead of counting an idle window as a pass.
            return new ProbeRun(Completed: false, Aborted: false, samples, NoLoadReasonKey);
        }

        return new ProbeRun(Completed: true, Aborted: false, samples);
    }
}
