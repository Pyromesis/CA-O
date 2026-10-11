using CAO.Core.Overclock.Stability;
using CAO.Infrastructure.Overclock;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

/// <summary>
/// The real probe against the real WMI on this machine. It asserts shape, not
/// values: clocks differ per machine, but a run either completes with evidence
/// or degrades to an incomplete window (which the evaluator grades Pending).
/// </summary>
public sealed class WmiClockStallProbeTests
{
    [Fact]
    public async Task Run_CompletesWithClockEvidence_OrDegradesWithoutThrowing()
    {
        var probe = new WmiClockStallProbe();
        var request = new ProbeRequest(TimeSpan.FromSeconds(2), [ProbeMetric.EffectiveClockStall]);

        var run = await probe.RunAsync(request, CancellationToken.None);

        Assert.NotNull(run);
        if (run.Completed)
        {
            Assert.NotEmpty(run.Samples);
            Assert.All(run.Samples, s => Assert.Contains(s.Evidence, e => e is ProbeEvidence.ClockStall));
            // Idle silicon passes through the load gate; a loaded machine that is
            // genuinely throttling reports it. Both prove the evidence flows.
            var verdict = StabilityEvaluator.Evaluate(run, StabilityThresholds.Default);
            Assert.True(
                verdict is StabilityVerdict.Passed or StabilityVerdict.FailedUnstable,
                $"Unexpected verdict {verdict} over a completed window.");
        }
        else
        {
            Assert.Equal(StabilityVerdict.Pending, StabilityEvaluator.Evaluate(run, StabilityThresholds.Default));
        }
    }

    [Fact]
    public async Task UnsupportedMetrics_ComeBackAsSensorUnavailable_NeverAsFaults()
    {
        var probe = new WmiClockStallProbe();
        var request = new ProbeRequest(
            TimeSpan.FromSeconds(1),
            [ProbeMetric.EffectiveClockStall, ProbeMetric.WheaEvents, ProbeMetric.KernelBugcheck]);

        var run = await probe.RunAsync(request, CancellationToken.None);

        var unavailable = run.Samples
            .SelectMany(s => s.Evidence)
            .OfType<ProbeEvidence.SensorUnavailable>()
            .ToList();
        Assert.Equal(2, unavailable.Count);
        Assert.All(unavailable, u => Assert.False(u.IsFault));
    }

    [Fact]
    public void Id_IsStable_ForJournalLines()
    {
        Assert.Equal("wmi-clock-stall", new WmiClockStallProbe().Id);
    }
}
