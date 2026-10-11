using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CAO.Core.Overclock;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Stability;
using CAO.Shared;

namespace CAO.Core.Tests.OverclockFixtures;

/// <summary>
/// Backend de mentira completo. El motor de tuning y el recovery se prueban contra esto:
/// ningun test de esta suite necesita GPU, CPU ni privilegios de administrador.
/// </summary>
internal class FakeOverclockBackend : ITuningTarget
{
    private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);

    public FakeOverclockBackend(string backendId = "fake-backend", OverclockComponent component = OverclockComponent.Gpu)
    {
        BackendId = backendId;
        Component = component;
    }

    public string BackendId { get; }

    public OverclockComponent Component { get; }

    public string? DriverVersion { get; set; } = "999.99";

    public List<string> AppliedWrites { get; } = [];

    public List<string> RestoredValues { get; } = [];

    public int CaptureCount { get; private set; }

    public void SetValue(string knobId, double value) => _values[knobId] = value;

    public double? Peek(string knobId) => _values.TryGetValue(knobId, out var v) ? v : null;

    public virtual ValueTask<KnobWriteResult> ApplyAsync(KnobId id, KnobValue value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _values[id.Value] = value.Scalar;
        AppliedWrites.Add(Format(id.Value, value.Scalar));
        return ValueTask.FromResult(KnobWriteResult.Ok(value));
    }

    public virtual ValueTask<UnitSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CaptureCount++;

        var values = _values
            .Select(pair => new KeyValuePair<KnobId, KnobValue>(new KnobId(pair.Key), KnobValue.FromScalar(pair.Value)))
            .ToList();

        return ValueTask.FromResult(new UnitSnapshot(Component, values, DateTimeOffset.UnixEpoch, DriverVersion));
    }

    public virtual ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var pair in snapshot.Values)
        {
            _values[pair.Key.Value] = pair.Value.Scalar;
            RestoredValues.Add(Format("revert:" + pair.Key.Value, pair.Value.Scalar));
        }

        return ValueTask.FromResult(RestoreOutcome.Ok(snapshot.Count, verified: true));
    }

    public virtual ValueTask<KnobWriteResult> RestoreValueAsync(KnobId id, KnobValue value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _values[id.Value] = value.Scalar;
        RestoredValues.Add(Format("revert:" + id.Value, value.Scalar));
        return ValueTask.FromResult(KnobWriteResult.Ok(value));
    }

    protected static string Format(string knob, double value) =>
        $"{knob}={value.ToString("0.##", CultureInfo.InvariantCulture)}";
}

/// <summary>Falla la escritura N veces antes de dejar de simular fallo.</summary>
internal sealed class FlakyBackend : FakeOverclockBackend, ITuningTarget
{
    public FlakyBackend(int failuresBeforeSuccess, string backendId = "flaky-backend")
        : base(backendId)
        => FailuresBeforeSuccess = failuresBeforeSuccess;

    public int FailuresBeforeSuccess { get; }

    public int WriteAttempts { get; private set; }

    public override ValueTask<KnobWriteResult> ApplyAsync(KnobId id, KnobValue value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteAttempts++;

        return WriteAttempts <= FailuresBeforeSuccess
            ? ValueTask.FromResult(KnobWriteResult.Fail("flaky-transient-write-failure"))
            : base.ApplyAsync(id, value, cancellationToken);
    }
}

/// <summary>
/// El driver instalado rechaza el setter. DriverBlocked es lo que impide que CA-O reintente
/// a ciegas contra una version de driver que no soporta la operacion.
/// </summary>
internal sealed class DriverBlockedBackend : FakeOverclockBackend, ITuningTarget
{
    public DriverBlockedBackend(string reasonKey = "nvapi-driver-blocked", string backendId = "blocked-backend")
        : base(backendId)
        => ReasonKey = reasonKey;

    public string ReasonKey { get; }

    public override ValueTask<KnobWriteResult> ApplyAsync(KnobId id, KnobValue value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(KnobWriteResult.Blocked(ReasonKey));
    }
}

/// <summary>
/// Probe de estabilidad guionizado: cada paso devuelve el <see cref="ProbeRun"/> que el test
/// le indica. Al agotar la lista devuelve el ultimo, para que un test que no cuenta bien los
/// pasos falle de forma visible en vez de colarse.
/// </summary>
internal sealed class ScriptedProbe : IStabilityProbe
{
    private readonly IReadOnlyList<ProbeRun> _script;

    public ScriptedProbe(params ProbeRun[] script) => _script = script;

    public string Id => "scripted-probe";

    public IReadOnlyList<ProbeMetric> SupportedMetrics { get; } =
    [
        ProbeMetric.WheaEvents,
        ProbeMetric.KernelBugcheck,
        ProbeMetric.EffectiveClockStall,
        ProbeMetric.GpuThermalLimit,
    ];

    public List<ProbeRequest> Requests { get; } = [];

    public Task<ProbeRun> RunAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);

        var index = Requests.Count - 1;
        return Task.FromResult(index < _script.Count ? _script[index] : _script[^1]);
    }

    public static ProbeSample Sample(params ProbeEvidence[] evidence) =>
        new(DateTimeOffset.UnixEpoch, evidence);

    public static ProbeRun Clean() => new(true, false, [Sample()]);

    public static ProbeRun WithFault(ProbeEvidence fault) => new(true, false, [Sample(fault)]);

    public static ProbeRun Aborted(string reasonKey = "probe-aborted") => ProbeRun.AbortedRun(reasonKey);

    public static ProbeRun Incomplete() => ProbeRun.NotStarted();
}

/// <summary>Journal que solo registra. Los tests afirman sobre la secuencia de fases.</summary>
internal sealed class RecordingJournal : IOverclockJournal
{
    public List<TransactionPhase> Phases { get; } = [];

    public List<string?> Details { get; } = [];

    public ValueTask WriteAsync(TransactionPhase phase, string? detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Phases.Add(phase);
        Details.Add(detail);
        return ValueTask.CompletedTask;
    }

    public bool Contains(TransactionPhase phase) => Phases.Contains(phase);
}

internal sealed class StubBootHealth : IBootHealthProbe
{
    private readonly BootHealthReport _report;

    public StubBootHealth(BootHealthReport report) => _report = report;

    public Task<BootHealthReport> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_report);
}

internal sealed class StubTargetResolver : ITuningTargetResolver
{
    private readonly Dictionary<string, ITuningTarget> _targets = new(StringComparer.Ordinal);

    public StubTargetResolver Register(string backendId, ITuningTarget target)
    {
        _targets[backendId] = target;
        return this;
    }

    public bool TryResolve(string backendId, out ITuningTarget? target) =>
        _targets.TryGetValue(backendId, out target);
}

/// <summary>Store en memoria: aísla los tests de D4 del sistema de ficheros.</summary>
internal sealed class InMemoryCheckpointStore : ITuningCheckpointStore
{
    private readonly Dictionary<Guid, TuningCheckpoint> _items = [];

    public List<Guid> Quarantined { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public IReadOnlyCollection<TuningCheckpoint> Items => _items.Values;

    public TuningCheckpoint? Get(Guid runId) => _items.GetValueOrDefault(runId);

    public Task<IReadOnlyList<TuningCheckpoint>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TuningCheckpoint>>(_items.Values.ToList());

    public Task SaveAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        _items[checkpoint.RunId] = checkpoint;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken)
    {
        Deleted.Add(runId);
        return Task.FromResult(_items.Remove(runId));
    }

    public Task<string?> QuarantineAsync(Guid runId, string reasonKey, CancellationToken cancellationToken)
    {
        Quarantined.Add(runId);
        return Task.FromResult<string?>($"in-memory://{runId:D}.corrupt-{reasonKey}");
    }
}

internal static class OverclockTestKnobs
{
    public static OverclockKnob Scalar(
        string id = "test.gpu.offset",
        OverclockComponent component = OverclockComponent.Gpu,
        double min = -100,
        double max = 500,
        double step = 10,
        EvidenceLevel evidence = EvidenceLevel.Vendor,
        IReadOnlyList<KnobDependency>? dependsOn = null) =>
        new(
            new KnobId(id),
            component,
            KnobKind.Scalar,
            KnobRange.Scalar(min, max),
            step,
            KnobUnit.Hertz,
            RiskLevel.Moderate,
            evidence,
            "knob_test",
            "knob_test_desc",
            false,
            KnobScope.Session,
            "test-backend",
            [],
            dependsOn ?? []);

    public static TuningRequest Request(
        Guid runId,
        OverclockKnob knob,
        ITuningTarget target,
        IStabilityProbe probe,
        double start,
        double ceiling,
        double step,
        TimeSpan? duration = null) =>
        new(
            runId,
            knob,
            target,
            probe,
            ProposedCeiling: ceiling,
            Step: step,
            StepDuration: duration ?? TimeSpan.FromSeconds(10),
            Thresholds: StabilityThresholds.Default,
            Metrics: [ProbeMetric.WheaEvents, ProbeMetric.KernelBugcheck, ProbeMetric.EffectiveClockStall])
        {
            StartingValue = start,
            Ceiling = ceiling,
        };

    public static TuningCheckpoint Checkpoint(
        Guid runId,
        CheckpointPhase phase = CheckpointPhase.StepApplied,
        double applied = 30,
        double lastKnownGood = 10,
        int consecutiveUncleanBoots = 0,
        string? driverVersion = "999.99",
        string knob = "test.gpu.offset",
        string backendId = "fake-backend") =>
        new()
        {
            RunId = runId,
            BackendId = backendId,
            Knob = knob,
            Component = nameof(OverclockComponent.Gpu),
            AppliedValue = applied,
            LastKnownGoodValue = lastKnownGood,
            StableCeiling = null,
            Phase = phase,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            DriverVersion = driverVersion,
            ConsecutiveUncleanBoots = consecutiveUncleanBoots,
        };

    public static string Describe(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}