using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Core.Overclock.Stability;
using CAO.Infrastructure.Overclock;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

/// <summary>
/// Regresiones de la auditoría del overclock (2026-10-13): versión de la struct de
/// offsets NVML, unidades del límite de potencia de AMD, sonda de estabilidad de GPU
/// y prioridad del backend de CPU.
/// </summary>
public sealed class OverclockCorrectnessTests
{
    [Fact]
    public void NvmlClockOffset_UsesTheOnlyVersionNvmlDeclares()
    {
        // nvml.h: nvmlClockOffset_v1 = sizeof(nvmlClockOffset_v1_t) | (1 << 24), 24 bytes.
        // 16|1<<24 y 24|2<<24 no existen: el driver los rechazaba y los offsets nunca
        // llegaban a estar disponibles.
        Assert.Equal(0x01000018u, NvmlInterop.ClockOffsetVersion);
        Assert.Equal(24, System.Runtime.InteropServices.Marshal.SizeOf<NvmlInterop.NvmlClockOffset>());
    }

    [Theory]
    [InlineData(-10, 15, null, 0, 100)]   // Adrenalin: offset alrededor de 0
    [InlineData(-10, 15, null, 15, 115)]
    [InlineData(-10, 15, 0, -10, 90)]
    [InlineData(50, 150, 100, 100, 100)]  // rango absoluto en % del TDP
    [InlineData(50, 150, null, 120, 120)]
    public void AmdPowerLimit_IsShownAsPercentOfStock(int min, int max, int? stock, int driver, double catalog)
    {
        var device = new AmdTuningDevice(null, min, max, stock);

        Assert.Equal(catalog, AmdAdlxBackend.ToCatalogPercent(driver, device));
        Assert.Equal(driver, AmdAdlxBackend.ToDriverPercent(catalog, device));
    }

    [Fact]
    public async Task AmdConservativeProfile_WritesStock_NotTheMaximum()
    {
        // Antes: el preset 100 se recortaba a +15 en un rango -10..+15 y el perfil
        // "Conservador" ponía la tarjeta al máximo.
        var api = new AmdAdlxBackendTests.FakeAmdTuningApi
        {
            Device = new AmdTuningDevice(null, -10, 15, 0),
            PowerPercent = 0,
        };
        var backend = new AmdAdlxBackend(api);

        var conservative = await backend.WriteAsync(AmdKnobs.PowerLimit, 100d, CancellationToken.None);
        var performance = await backend.WriteAsync(AmdKnobs.PowerLimit, 120d, CancellationToken.None);

        Assert.True(conservative.Success);
        Assert.Equal("power=0", api.Writes[0]);
        Assert.True(performance.Success);
        Assert.Equal("power=15", api.Writes[1]);
        Assert.Equal(115d, performance.Applied!.Value.Scalar);
    }

    [Fact]
    public async Task GpuProbe_IdleCard_GivesNoVerdict()
    {
        var probe = new NvmlGpuStabilityProbe(new FakeTelemetry { Load = 5 }, TimeSpan.Zero);

        var run = await probe.RunAsync(new ProbeRequest(TimeSpan.FromSeconds(5), []), CancellationToken.None);

        Assert.Equal(StabilityVerdict.Pending, StabilityEvaluator.Evaluate(run, StabilityThresholds.Default));
        Assert.Equal(NvmlGpuStabilityProbe.NoLoadReasonKey, run.AbortReasonKey);
    }

    [Fact]
    public async Task GpuProbe_LoadedCard_Passes()
    {
        var probe = new NvmlGpuStabilityProbe(new FakeTelemetry { Load = 98, Temperature = 70, Slowdown = 90 }, TimeSpan.Zero);

        var run = await probe.RunAsync(new ProbeRequest(TimeSpan.FromSeconds(5), []), CancellationToken.None);

        Assert.Equal(StabilityVerdict.Passed, StabilityEvaluator.Evaluate(run, StabilityThresholds.Default));
    }

    [Fact]
    public async Task GpuProbe_DriverReset_IsAFault()
    {
        var probe = new NvmlGpuStabilityProbe(new FakeTelemetry { Load = 98, LoseAfter = 2 }, TimeSpan.Zero);

        var run = await probe.RunAsync(new ProbeRequest(TimeSpan.FromSeconds(5), []), CancellationToken.None);

        Assert.Equal(StabilityVerdict.FailedUnstable, StabilityEvaluator.Evaluate(run, StabilityThresholds.Default));
        Assert.Contains(run.Samples.SelectMany(s => s.Evidence), e => e is ProbeEvidence.DeviceLost);
    }

    [Fact]
    public async Task GpuProbe_SlowdownTemperature_IsAFault()
    {
        var probe = new NvmlGpuStabilityProbe(new FakeTelemetry { Load = 98, Temperature = 92, Slowdown = 90 }, TimeSpan.Zero);

        var run = await probe.RunAsync(new ProbeRequest(TimeSpan.FromSeconds(3), []), CancellationToken.None);

        Assert.Equal(StabilityVerdict.FailedUnstable, StabilityEvaluator.Evaluate(run, StabilityThresholds.Default));
    }

    [Fact]
    public void Registry_DetectedXtu_IsNotOverwrittenByTheGpuBackendsPolicyAnswer()
    {
        var registry = new OverclockReadOnlyRegistry(
            new CpuKindBackend("cpu-xtu", CpuBackendKind.XtuGuided),
            new CpuKindBackend("gpu-like", CpuBackendKind.PolicyOnly));

        Assert.Equal(CpuBackendKind.XtuGuided, registry.ProbeAll(CancellationToken.None).Cpu);
    }

    [Fact]
    public void Registry_WithAnNvidiaCard_DoesNotListAmdRows()
    {
        var registry = new OverclockReadOnlyRegistry(new NvidiaLikeBackend());

        var knobs = registry.ProbeAll(CancellationToken.None).Knobs;

        Assert.DoesNotContain(knobs, k => k.BackendId == OverclockCatalog.AmdAdlxBackendId);
        Assert.Contains(knobs, k => k.Component == OverclockComponent.Cpu);
    }

    private sealed class FakeTelemetry : IGpuStabilityTelemetry
    {
        private int _clockReads;

        public uint Load { get; init; }

        public uint Temperature { get; init; } = 60;

        public uint Slowdown { get; init; }

        public int LoseAfter { get; init; } = int.MaxValue;

        public bool TryResolve() => true;

        public bool TryGetGraphicsClock(out uint megahertz)
        {
            megahertz = 1800;
            return ++_clockReads <= LoseAfter;
        }

        public bool TryGetUtilization(out uint percent)
        {
            percent = Load;
            return true;
        }

        public bool TryGetTemperature(out uint celsius)
        {
            celsius = Temperature;
            return true;
        }

        public bool TryGetSlowdownTemperature(out uint celsius)
        {
            celsius = Slowdown;
            return Slowdown > 0;
        }
    }

    private sealed class CpuKindBackend(string id, CpuBackendKind kind) : IOverclockBackend
    {
        public string Id => id;

        public OverclockCapabilities Probe(CancellationToken ct) => new(
            kind,
            GpuBackendKind.None,
            MemoryBackendKind.None,
            [new BackendAvailability(id, true, string.Empty, null, false, null, EvidenceLevel.Official)],
            []);

        public ValueTask<KnobRead> ReadAsync(KnobId knobId, CancellationToken ct) => new(KnobRead.Fail("x"));

        public ValueTask<KnobWriteResult> WriteAsync(KnobId knobId, KnobValue value, CancellationToken ct) => new(KnobWriteResult.Fail("x"));

        public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) =>
            new(new UnitSnapshot(OverclockComponent.Cpu, [], DateTimeOffset.UnixEpoch, null));

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) => new(RestoreOutcome.Ok(0, true));
    }

    private sealed class NvidiaLikeBackend : IOverclockBackend
    {
        public string Id => OverclockCatalog.NvapiBackendId;

        public OverclockCapabilities Probe(CancellationToken ct) => new(
            CpuBackendKind.PolicyOnly,
            GpuBackendKind.NvidiaNvapi,
            MemoryBackendKind.None,
            [new BackendAvailability(Id, true, string.Empty, null, false, "999", EvidenceLevel.Vendor)],
            OverclockCatalog.NvidiaGpuKnobs);

        public ValueTask<KnobRead> ReadAsync(KnobId knobId, CancellationToken ct) => new(KnobRead.Fail("x"));

        public ValueTask<KnobWriteResult> WriteAsync(KnobId knobId, KnobValue value, CancellationToken ct) => new(KnobWriteResult.Fail("x"));

        public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) =>
            new(new UnitSnapshot(OverclockComponent.Gpu, [], DateTimeOffset.UnixEpoch, null));

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) => new(RestoreOutcome.Ok(0, true));
    }
}
