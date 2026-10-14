using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Core.Overclock.Policy;
using CAO.Core.Overclock.Stability;
using CAO.Core.Tests.OverclockFixtures;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Regresiones de la auditoría del overclock (2026-10-13). Cada test fija un fallo
/// que llegó a producción: el turbo apagado por el perfil Rendimiento, la ventana
/// sin veredicto contada como aprobada y el recorrido que seguía "subiendo" un
/// valor que el driver ya recortaba.
/// </summary>
public sealed class OverclockCorrectnessTests
{
    [Fact]
    public void DiscreteValues_CarryTheirIndexAsScalar_SoBackendsWriteTheRightNumber()
    {
        // powercfg recibe el número crudo. Con Scalar=0, el preset "turbo agresivo"
        // (índice 2) escribía PERFBOOSTMODE=0: turbo APAGADO, reportado como éxito.
        var aggressive = KnobValue.FromIndex(2);

        Assert.Equal(2, aggressive.Index);
        Assert.Equal(2d, aggressive.Scalar);
    }

    [Fact]
    public void NoProfile_TurnsCpuTurboOff()
    {
        var boost = OverclockCatalog.CpuPolicyKnobs.Single(k => k.Id == IntelPowerPolicy.BoostModeKnob);

        foreach (var preset in boost.Presets)
        {
            Assert.NotEqual((int)CpuBoostMode.Disabled, (int)preset.Value.Scalar);
        }

        Assert.Equal((int)CpuBoostMode.Aggressive, (int)boost.PresetValue(OverclockProfile.Performance)!.Value.Scalar);
    }

    [Fact]
    public async Task PendingVerdict_IsNotAPass_RestoresLastGood_AndReportsUnverified()
    {
        var knob = OverclockTestKnobs.Scalar(step: 10);
        var backend = new FakeOverclockBackend();
        backend.SetValue(knob.Id.Value, 0);
        var probe = new ScriptedProbe(new ProbeRun(false, false, [ScriptedProbe.Sample()], "overclock-probe-gpu-no-load"));
        var journal = new RecordingJournal();

        var result = await new TuningEngine(journal).RunAsync(
            OverclockTestKnobs.Request(Guid.NewGuid(), knob, backend, probe, start: 0, ceiling: 50, step: 10));

        Assert.Equal(TuningStatus.Unverified, result.Status);
        Assert.Equal("overclock-probe-gpu-no-load", result.ReasonKey);
        Assert.Null(result.StableCeiling);
        Assert.Single(result.Points);
        Assert.Equal(0d, backend.Peek(knob.Id.Value));
        Assert.DoesNotContain(TransactionPhase.OverclockTestPassed, journal.Phases);
    }

    [Fact]
    public async Task DriverClamp_StopsTheWalk_AtTheValueTheHardwareActuallyTook()
    {
        var knob = OverclockTestKnobs.Scalar(step: 10);
        var backend = new ClampingBackend(maxAccepted: 20);
        backend.SetValue(knob.Id.Value, 0);

        var result = await new TuningEngine().RunAsync(
            OverclockTestKnobs.Request(Guid.NewGuid(), knob, backend, new ScriptedProbe(ScriptedProbe.Clean()), start: 0, ceiling: 100, step: 10));

        Assert.Equal(TuningStatus.CeilingReached, result.Status);
        Assert.Equal(20d, result.AppliedValue.Scalar);
        Assert.Equal(20d, result.StableCeiling);

        // 0, 10, 20 probados; el cuarto intento (30 -> recortado a 20) corta el recorrido.
        Assert.Equal(3, result.Points.Count);
    }

    private sealed class ClampingBackend(double maxAccepted) : FakeOverclockBackend
    {
        public override ValueTask<KnobWriteResult> ApplyAsync(KnobId id, KnobValue value, CancellationToken cancellationToken)
        {
            var taken = KnobValue.FromScalar(Math.Min(value.Scalar, maxAccepted));
            return base.ApplyAsync(id, taken, cancellationToken);
        }
    }
}
