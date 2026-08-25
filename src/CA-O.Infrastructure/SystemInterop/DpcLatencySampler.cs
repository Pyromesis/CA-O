using System.Diagnostics;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>DPC/ISR pressure observed on one logical processor.</summary>
public sealed record ProcessorInterruptSample(
    string Instance,
    double MaxDpcTimePercent,
    double MaxInterruptTimePercent);

/// <summary>
/// System-wide interrupt pressure over a sampling window.
/// Per-driver attribution requires an ETW kernel trace and is intentionally
/// out of scope here (documented limitation); this sampler answers "is there
/// a DPC/ISR problem at all, and how severe".
/// </summary>
public sealed record InterruptPressureReport(
    TimeSpan Window,
    IReadOnlyList<ProcessorInterruptSample> Samples,
    double TotalMaxDpcPercent,
    double TotalMaxInterruptPercent)
{
    /// <summary>Rough severity heuristic used by diagnostics UI.</summary>
    public string SeverityEs =>
        TotalMaxDpcPercent >= 25 ? "Alta" :
        TotalMaxDpcPercent >= 10 ? "Media" : "Baja";
}

/// <summary>
/// Samples "% DPC Time" and "% Interrupt Time" performance counters per CPU
/// over a short window (spec 34). No external processes are spawned.
/// </summary>
public sealed class DpcLatencySampler
{
    public async Task<InterruptPressureReport> SampleAsync(
        TimeSpan? window = null,
        CancellationToken ct = default)
    {
        var duration = window ?? TimeSpan.FromSeconds(5);
        return await Task.Run(() =>
        {
            var dpcCounters = new List<PerformanceCounter>();
            var interruptCounters = new List<PerformanceCounter>();
            var instances = new List<string>();

            try
            {
                var category = new PerformanceCounterCategory("Processor");
                foreach (var instance in category.GetInstanceNames())
                {
                    dpcCounters.Add(new PerformanceCounter("Processor", "% DPC Time", instance, readOnly: true));
                    interruptCounters.Add(new PerformanceCounter("Processor", "% Interrupt Time", instance, readOnly: true));
                    instances.Add(instance);
                }

                foreach (var counter in dpcCounters.Concat(interruptCounters))
                {
                    counter.NextValue(); // prime baseline
                }

                Thread.Sleep(duration);

                var maxDpc = new Dictionary<string, double>(StringComparer.Ordinal);
                var maxInterrupt = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (var counter in dpcCounters)
                {
                    maxDpc[counter.InstanceName] = counter.NextValue();
                }

                foreach (var counter in interruptCounters)
                {
                    maxInterrupt[counter.InstanceName] = counter.NextValue();
                }

                var samples = instances.Select(instance => new ProcessorInterruptSample(
                        instance,
                        Math.Round(maxDpc.GetValueOrDefault(instance), 2),
                        Math.Round(maxInterrupt.GetValueOrDefault(instance), 2)))
                    .ToList();

                var totalDpc = samples.FirstOrDefault(sample => sample.Instance == "_Total")?.MaxDpcTimePercent ?? 0;
                var totalInterrupt = samples.FirstOrDefault(sample => sample.Instance == "_Total")?.MaxInterruptTimePercent ?? 0;

                return new InterruptPressureReport(
                    duration,
                    samples,
                    totalDpc,
                    totalInterrupt);
            }
            finally
            {
                foreach (var counter in dpcCounters.Concat(interruptCounters))
                {
                    counter.Dispose();
                }
            }
        }, ct);
    }
}
