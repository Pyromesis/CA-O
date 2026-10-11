using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Stability;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests.OverclockFixtures;

/// <summary>
/// Cubre la debilidad que Hypertune no cubre: despues de un cuelgue, la maquina vuelve
/// estable sin que nadie toque nada. Estas pruebas son deterministas porque el arranque
/// sucio/limpio es una abstraccion, no una lectura del registro de Windows.
/// </summary>
public sealed class OverclockRecoveryTests
{
    [Fact]
    public async Task Recovery_DoesNotAutoRevert_OnFirstUncleanBoot()
    {
        var runId = Guid.NewGuid();
        var backend = new FakeOverclockBackend();
        backend.SetValue("test.gpu.offset", 30);

        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OverclockTestKnobs.Checkpoint(runId, consecutiveUncleanBoots: 0), CancellationToken.None);

        var journal = new RecordingJournal();
        var service = new OverclockRecoveryService(
            store,
            new StubBootHealth(BootHealthReport.Unclean("bugcheck-7e")),
            new StubTargetResolver().Register("fake-backend", backend),
            journal);

        var outcomes = await service.RecoverAllAsync(CancellationToken.None);

        var outcome = Assert.Single(outcomes);

        // Un solo cuelgue no es evidencia de que el ajuste sea culpable: puede haber sido
        // cualquier otra cosa. Revertir aqui perderia el progreso sin motivo.
        Assert.Equal(RecoveryAction.NotifyUser, outcome.Action);
        Assert.Equal(CheckpointPhase.Interrupted, outcome.Phase);
        Assert.True(outcome.NeedsUserDecision);
        Assert.Null(outcome.RevertSucceeded);

        // La prueba dura: el valor NO se toco.
        Assert.Empty(backend.RestoredValues);
        Assert.Equal(30, backend.Peek("test.gpu.offset"));

        // Y el contador SI se incremento, porque vive en el fichero y sobrevive al reinicio.
        Assert.Equal(1, store.Get(runId)!.ConsecutiveUncleanBoots);
        Assert.True(journal.Contains(TransactionPhase.OverclockInterrupted));
    }

    [Fact]
    public async Task Recovery_AutoReverts_OnSecondConsecutiveUncleanBoot()
    {
        var runId = Guid.NewGuid();
        var backend = new FakeOverclockBackend();
        backend.SetValue("test.gpu.offset", 30);

        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OverclockTestKnobs.Checkpoint(runId, consecutiveUncleanBoots: 1, lastKnownGood: 10), CancellationToken.None);

        var service = new OverclockRecoveryService(
            store,
            new StubBootHealth(BootHealthReport.Unclean("bugcheck-7e")),
            new StubTargetResolver().Register("fake-backend", backend));

        var outcome = Assert.Single(await service.RecoverAllAsync(CancellationToken.None));

        Assert.Equal(RecoveryAction.AutoRevert, outcome.Action);
        Assert.Equal(CheckpointPhase.AutoReverted, outcome.Phase);
        Assert.True(outcome.RevertSucceeded);

        // Dos cuelgues en el mismo valor es evidencia suficiente: se vuelve al ultimo
        // valor que SI paso la ventana de estabilidad.
        Assert.Equal(10, backend.Peek("test.gpu.offset"));
        Assert.Contains(backend.RestoredValues, v => v.Contains("test.gpu.offset"));

        var saved = store.Get(runId)!;
        Assert.Equal(CheckpointPhase.AutoReverted, saved.Phase);
        Assert.Equal(0, saved.ConsecutiveUncleanBoots);
        Assert.True(saved.IsTerminal);
    }

    [Fact]
    public async Task Recovery_DoesNotRun_WhenLastBootWasClean()
    {
        var runId = Guid.NewGuid();
        var backend = new FakeOverclockBackend();
        backend.SetValue("test.gpu.offset", 30);

        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OverclockTestKnobs.Checkpoint(runId, consecutiveUncleanBoots: 1), CancellationToken.None);

        var service = new OverclockRecoveryService(
            store,
            new StubBootHealth(BootHealthReport.Clean),
            new StubTargetResolver().Register("fake-backend", backend));

        var outcomes = await service.RecoverAllAsync(CancellationToken.None);

        // Arranque limpio: el equipo arranco y el usuario llego hasta aqui. Nada que hacer.
        Assert.Empty(outcomes);
        Assert.Empty(backend.RestoredValues);
        Assert.Equal(30, backend.Peek("test.gpu.offset"));

        // El contador SI se pone a cero. Sin esto, un cuelgue hoy y otro dentro de un mes
        // sumarian dos y provocarian un revert que nadie ha pedido.
        Assert.Equal(0, store.Get(runId)!.ConsecutiveUncleanBoots);
    }

    [Fact]
    public async Task Recovery_DoesNotAutoRevert_WhenDriverChanged()
    {
        var runId = Guid.NewGuid();
        var backend = new FakeOverclockBackend { DriverVersion = "1000.01" };
        backend.SetValue("test.gpu.offset", 30);

        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(
            OverclockTestKnobs.Checkpoint(runId, consecutiveUncleanBoots: 1, driverVersion: "999.99"),
            CancellationToken.None);

        var service = new OverclockRecoveryService(
            store,
            new StubBootHealth(BootHealthReport.Unclean("bugcheck-116")),
            new StubTargetResolver().Register("fake-backend", backend));

        var outcome = Assert.Single(await service.RecoverAllAsync(CancellationToken.None));

        // El driver cambio entre el paso y el arranque. "LastKnownGood" ya no significa lo
        // mismo, asi que se degrada a aviso en vez de escribir a ciegas.
        Assert.Equal(RecoveryAction.NotifyUser, outcome.Action);
        Assert.Equal(RecoveryPolicy.ReasonDriverChanged, outcome.ReasonKey);
        Assert.Empty(backend.RestoredValues);
    }

    [Fact]
    public async Task Recovery_KeepsRunOpen_WhenRevertFails()
    {
        var runId = Guid.NewGuid();
        var backend = new RevertFailingBackend("fake-backend");

        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OverclockTestKnobs.Checkpoint(runId, consecutiveUncleanBoots: 1), CancellationToken.None);

        var service = new OverclockRecoveryService(
            store,
            new StubBootHealth(BootHealthReport.Unclean("bugcheck-7e")),
            new StubTargetResolver().Register("fake-backend", backend));

        var outcome = Assert.Single(await service.RecoverAllAsync(CancellationToken.None));

        // El revert automatico no pudo aplicarse. El run NO se cierra: fingir un rollback
        // limpio cuando no lo hubo es exactamente el fallo que hay que evitar.
        Assert.Equal(RecoveryAction.NotifyUser, outcome.Action);
        Assert.Equal(CheckpointPhase.Interrupted, outcome.Phase);
        Assert.False(outcome.RevertSucceeded);

        var saved = store.Get(runId)!;
        Assert.False(saved.IsTerminal);
    }

    [Fact]
    public async Task Recovery_NotifiesUser_WhenBackendNoLongerResolves()
    {
        var runId = Guid.NewGuid();
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(OverclockTestKnobs.Checkpoint(runId, consecutiveUncleanBoots: 1), CancellationToken.None);

        var service = new OverclockRecoveryService(
            store,
            new StubBootHealth(BootHealthReport.Unclean("bugcheck-7e")),
            new StubTargetResolver());

        var outcome = Assert.Single(await service.RecoverAllAsync(CancellationToken.None));

        // Sin backend no hay con qué revertir. El motivo tiene que ser ese y no
        // "el driver ha cambiado": son总支otas y solo una arregla reinstalando el driver.
        Assert.Equal(RecoveryAction.NotifyUser, outcome.Action);
        Assert.Equal(OverclockRecoveryService.ReasonBackendUnavailable, outcome.ReasonKey);
        Assert.Equal(CheckpointPhase.Interrupted, store.Get(runId)!.Phase);
    }

    private sealed class RevertFailingBackend : FakeOverclockBackend, ITuningTarget
    {
        public RevertFailingBackend(string backendId)
            : base(backendId)
        {
        }

        public override ValueTask<KnobWriteResult> RestoreValueAsync(KnobId id, KnobValue value, CancellationToken cancellationToken) =>
            ValueTask.FromResult(KnobWriteResult.Blocked("nvapi-driver-blocked"));
    }

    [Fact]
    public async Task Recovery_SkipsTerminalCheckpoints()
    {
        var backend = new FakeOverclockBackend();
        var store = new InMemoryCheckpointStore();

        await store.SaveAsync(OverclockTestKnobs.Checkpoint(Guid.NewGuid(), CheckpointPhase.Closed), CancellationToken.None);
        await store.SaveAsync(OverclockTestKnobs.Checkpoint(Guid.NewGuid(), CheckpointPhase.AutoReverted), CancellationToken.None);

        var service = new OverclockRecoveryService(
            store,
            new StubBootHealth(BootHealthReport.Unclean("bugcheck-7e")),
            new StubTargetResolver().Register("fake-backend", backend));

        // Un run cerrado no es evidencia abierta: reventarlo en el siguiente arranque seria
        // tocar una maquina que ya quedo bien.
        Assert.Empty(await service.RecoverAllAsync(CancellationToken.None));
        Assert.Empty(backend.RestoredValues);
    }

    [Fact]
    public void RecoveryPolicy_CountsConsecutiveUncleanBoots()
    {
        var checkpoint = OverclockTestKnobs.Checkpoint(Guid.NewGuid(), consecutiveUncleanBoots: 0);

        var first = RecoveryPolicy.Decide(checkpoint, lastBootWasClean: false, currentDriverVersion: "999.99");
        Assert.Equal(RecoveryAction.NotifyUser, first.Action);
        Assert.Equal(1, first.ConsecutiveUncleanBoots);

        var second = RecoveryPolicy.Decide(checkpoint with { ConsecutiveUncleanBoots = 1 }, lastBootWasClean: false, currentDriverVersion: "999.99");
        Assert.Equal(RecoveryAction.AutoRevert, second.Action);
        Assert.Equal(2, second.ConsecutiveUncleanBoots);
    }

    [Fact]
    public void RecoveryPolicy_NeverActsOnUnknownDriverVersionAfterSecondFailure()
    {
        // Sin version de driver conocida no hay forma de saber si el registro de reloj
        // significa lo mismo. Abstenerse es lo correcto, no un bug.
        var checkpoint = OverclockTestKnobs.Checkpoint(Guid.NewGuid(), consecutiveUncleanBoots: 1, driverVersion: null);

        var decision = RecoveryPolicy.Decide(checkpoint, lastBootWasClean: false, currentDriverVersion: null);

        Assert.Equal(RecoveryAction.NotifyUser, decision.Action);
    }
}

/// <summary>Checkpoint store: escritura atomica y cuarentena que nunca borra evidencia.</summary>
public sealed class TuningCheckpointStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "cao-oc-ckpt-" + Guid.NewGuid().ToString("N"));

    public TuningCheckpointStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Un fichero abierto por el antivirus no puede impedir que el test pase.
        }
    }

    private FileTuningCheckpointStore Store() => new(_dir);

    [Fact]
    public async Task Checkpoint_RoundTrips_ThroughDisk()
    {
        var store = Store();
        var runId = Guid.NewGuid();
        var checkpoint = OverclockTestKnobs.Checkpoint(runId, consecutiveUncleanBoots: 2);

        await store.SaveAsync(checkpoint, CancellationToken.None);

        var loaded = Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.Equal(runId, loaded.RunId);
        Assert.Equal("fake-backend", loaded.BackendId);
        Assert.Equal(2, loaded.ConsecutiveUncleanBoots);
        Assert.Equal(30, loaded.AppliedValue);
        Assert.Equal(10, loaded.LastKnownGoodValue);
    }

    [Fact]
    public async Task Checkpoint_WritesAtomically_NoPartialFilesLeftBehind()
    {
        var store = Store();
        await store.SaveAsync(OverclockTestKnobs.Checkpoint(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        Assert.Single(Directory.GetFiles(_dir, "*.json"));
    }

    [Fact]
    public async Task Checkpoint_QuarantinesCorruptFile_NeverDeletesIt()
    {
        var store = Store();
        var runId = Guid.NewGuid();
        await store.SaveAsync(OverclockTestKnobs.Checkpoint(runId), CancellationToken.None);

        var path = Directory.GetFiles(_dir, "*.json").Single();
        await File.WriteAllTextAsync(path, "{ esto no es json", CancellationToken.None);

        Assert.Empty(await store.ListAsync(CancellationToken.None));

        // El fichero sigue en disco. Perder evidencia de un fallo es peor que tenerla sucia.
        var remaining = Directory.GetFiles(_dir);
        Assert.NotEmpty(remaining);
        Assert.Contains(remaining, f => f.Contains(".corrupt-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Checkpoint_IgnoresTransientIoErrorsWithoutQuarantine()
    {
        var store = Store();
        await store.SaveAsync(OverclockTestKnobs.Checkpoint(Guid.NewGuid()), CancellationToken.None);

        // Un directorio no se puede deserializar: es un error de E/S, no corrupcion.
        Directory.CreateDirectory(Path.Combine(_dir, "trap.json"));

        var loaded = await store.ListAsync(CancellationToken.None);
        Assert.Single(loaded);

        Assert.Empty(Directory.GetFiles(_dir, "*.corrupt-*"));
    }
}

/// <summary>El motor de tuning: bisección, revert a LastKnownGood y reanudacion.</summary>
public sealed class TuningEngineTests
{
    private static readonly KnobId Knob = new("test.gpu.offset");

    [Fact]
    public async Task Tuning_RestoresToLastKnownGood_WhenProbeReportsUnstable()
    {
        var backend = new FakeOverclockBackend();
        backend.SetValue(Knob.Value, 0);

        var journal = new RecordingJournal();
        var engine = new TuningEngine(journal);

        var probe = new ScriptedProbe(
            ScriptedProbe.Clean(),                                   // 0 pasa
            ScriptedProbe.Clean(),                                   // 10 pasa
            ScriptedProbe.WithFault(new ProbeEvidence.WheaErrors(3))); // 20 falla

        var request = OverclockTestKnobs.Request(
            Guid.NewGuid(),
            OverclockTestKnobs.Scalar(),
            backend,
            probe,
            start: 0,
            ceiling: 40,
            step: 10);

        var result = await engine.RunAsync(request, progress: null, CancellationToken.None);

        Assert.Equal(TuningStatus.StabilityCeilingFound, result.Status);

        // El techo real es el ultimo valor que SI paso, no el que fallo.
        Assert.Equal(10, result.StableCeiling);

        // Y la maquina NO se queda en el valor que acaba de fallar: ese es el contrato.
        Assert.Equal(10, backend.Peek(Knob.Value));

        Assert.True(journal.Contains(TransactionPhase.OverclockTestFailed));
        Assert.True(journal.Contains(TransactionPhase.OverclockStepRejected));
        Assert.Equal(2, result.Points.Count(p => p.Verdict == StabilityVerdict.Passed));
        Assert.Single(result.Points, p => p.Verdict == StabilityVerdict.FailedUnstable);
    }

    [Fact]
    public async Task Tuning_LeavesValueInPlace_WhenProbeAborted()
    {
        var backend = new FakeOverclockBackend();
        backend.SetValue(Knob.Value, 0);

        var journal = new RecordingJournal();
        var engine = new TuningEngine(journal);

        var probe = new ScriptedProbe(
            ScriptedProbe.Clean(),
            ScriptedProbe.Aborted("probe-time-budget-exhausted"));

        var request = OverclockTestKnobs.Request(
            Guid.NewGuid(),
            OverclockTestKnobs.Scalar(),
            backend,
            probe,
            start: 0,
            ceiling: 40,
            step: 10);

        var result = await engine.RunAsync(request, progress: null, CancellationToken.None);

        Assert.Equal(TuningStatus.Interrupted, result.Status);
        Assert.True(result.CanResume);
        Assert.Equal(10, result.ResumeFromValue);

        // Revvertir aqui destruiria el progreso y dejaria la maquina peor que al empezar.
        Assert.Empty(backend.RestoredValues);
        Assert.Equal(10, backend.Peek(Knob.Value));

        Assert.True(journal.Contains(TransactionPhase.OverclockInterrupted));
        Assert.False(journal.Contains(TransactionPhase.OverclockTestFailed));
    }

    [Fact]
    public async Task Tuning_NeverWritesATerminalPhase()
    {
        var journal = new RecordingJournal();
        var engine = new TuningEngine(journal);

        var probe = new ScriptedProbe(ScriptedProbe.Clean(), ScriptedProbe.Clean());
        var request = OverclockTestKnobs.Request(
            Guid.NewGuid(),
            OverclockTestKnobs.Scalar(),
            new FakeOverclockBackend(),
            probe,
            start: 0,
            ceiling: 20,
            step: 10);

        await engine.RunAsync(request, progress: null, CancellationToken.None);

        // Cerrar la transaccion es de la capa que la abrio. Si el motor escribiera Commit,
        // un tuning abortado podria disfrazarse de transaction completa.
        Assert.All(journal.Phases, phase => Assert.False(TransactionEvent.IsTerminal(phase)));
    }

    [Fact]
    public async Task Tuning_Rejects_WhenDriverBlocksTheWrite()
    {
        var backend = new DriverBlockedBackend();
        var journal = new RecordingJournal();
        var engine = new TuningEngine(journal);

        var probe = new ScriptedProbe(ScriptedProbe.Clean());
        var request = OverclockTestKnobs.Request(
            Guid.NewGuid(),
            OverclockTestKnobs.Scalar(),
            backend,
            probe,
            start: 0,
            ceiling: 40,
            step: 10);

        var result = await engine.RunAsync(request, progress: null, CancellationToken.None);

        // DriverBlocked no es un fallo transitorio: no se reintenta, la seccion pasa a solo lectura.
        Assert.Equal(TuningStatus.Rejected, result.Status);
        Assert.Equal("nvapi-driver-blocked", result.ReasonKey);
        Assert.Empty(probe.Requests);
        Assert.True(journal.Contains(TransactionPhase.OverclockStepRejected));
    }

    [Fact]
    public async Task Tuning_ReachesProposedCeiling_WhenEveryStepIsStable()
    {
        var backend = new FakeOverclockBackend();
        backend.SetValue(Knob.Value, 0);

        var engine = new TuningEngine();
        var probe = new ScriptedProbe(
            ScriptedProbe.Clean(), ScriptedProbe.Clean(), ScriptedProbe.Clean(), ScriptedProbe.Clean(), ScriptedProbe.Clean());

        var reported = new List<TuningProgress>();
        var request = OverclockTestKnobs.Request(
            Guid.NewGuid(),
            OverclockTestKnobs.Scalar(),
            backend,
            probe,
            start: 0,
            ceiling: 40,
            step: 10);

        var result = await engine.RunAsync(request, new Progress<TuningProgress>(reported.Add), CancellationToken.None);

        Assert.Equal(TuningStatus.CeilingReached, result.Status);
        Assert.Equal(40, result.AppliedValue.Scalar);
        Assert.Equal(40, backend.Peek(Knob.Value));
        Assert.Equal(5, result.Points.Count);
        Assert.All(result.Points, p => Assert.Equal(StabilityVerdict.Passed, p.Verdict));
    }

    [Fact]
    public async Task Tuning_Throws_WhenRevertToLastKnownGoodFails()
    {
        var backend = new RevertFailingBackend();
        var engine = new TuningEngine();

        var probe = new ScriptedProbe(
            ScriptedProbe.Clean(),
            ScriptedProbe.WithFault(new ProbeEvidence.KernelBugcheck("0x133", HardwareVerified: true)));

        var request = OverclockTestKnobs.Request(
            Guid.NewGuid(),
            OverclockTestKnobs.Scalar(),
            backend,
            probe,
            start: 0,
            ceiling: 40,
            step: 10);

        // Un revert fallido significa que no sabemos en que estado esta la maquina.
        // Propagar es lo unico honesto: el recovery service decide.
        var ex = await Assert.ThrowsAsync<TuningRestoreFailedException>(
            () => engine.RunAsync(request, progress: null, CancellationToken.None));

        Assert.Equal("revert-refused", ex.ReasonKey);
    }

    [Fact]
    public async Task Tuning_KeepsEvidenceAttachedToEachPoint()
    {
        var engine = new TuningEngine();
        var probe = new ScriptedProbe(
            ScriptedProbe.WithFault(new ProbeEvidence.ThermalLimitReached("gpu-core", 83, 87.5)),
            ScriptedProbe.Clean());

        var request = OverclockTestKnobs.Request(
            Guid.NewGuid(),
            OverclockTestKnobs.Scalar(),
            new FakeOverclockBackend(),
            probe,
            start: 0,
            ceiling: 40,
            step: 10);

        var result = await engine.RunAsync(request, progress: null, CancellationToken.None);

        // La evidencia viaja con el punto. No se colapsa a un numero: "el GPU se paso de 83
        // grados" y "el GPU se estabilizo" son afirmaciones distintas y auditables.
        var thermal = Assert.Single(result.Points.SelectMany(p => p.Evidence).OfType<ProbeEvidence.ThermalLimitReached>());
        Assert.Equal(87.5, thermal.PeakCelsius);
        Assert.Equal("gpu-core", thermal.SensorName);
    }

    [Fact]
    public async Task Tuning_RespectsCancellation()
    {
        var engine = new TuningEngine();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var request = OverclockTestKnobs.Request(
            Guid.NewGuid(),
            OverclockTestKnobs.Scalar(),
            new FakeOverclockBackend(),
            new ScriptedProbe(ScriptedProbe.Clean()),
            start: 0,
            ceiling: 40,
            step: 10);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => engine.RunAsync(request, progress: null, cts.Token));
    }

    private sealed class RevertFailingBackend : FakeOverclockBackend, ITuningTarget
    {
        public override ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken cancellationToken) =>
            ValueTask.FromResult(RestoreOutcome.Fail("revert-refused"));
    }
}

/// <summary>El evaluador: una ventana que no corrio NUNCA es un "aprobado".</summary>
public sealed class StabilityEvaluatorTests
{
    [Fact]
    public void Evaluator_ReportsPending_WhenTheRunNeverCompleted()
    {
        var verdict = StabilityEvaluator.Evaluate(ProbeRun.NotStarted(), StabilityThresholds.Default);

        // Este es el fallo que haria que Hypertune envie a un usuario a un valor que nunca
        // se ha probado: "estabilizado" sin haber corrido una sola ventana.
        Assert.Equal(StabilityVerdict.Pending, verdict);
    }

    [Fact]
    public void Evaluator_ReportsAborted_WhenTheProbeAborted()
    {
        var verdict = StabilityEvaluator.Evaluate(ProbeRun.AbortedRun("budget-exhausted"), StabilityThresholds.Default);

        Assert.Equal(StabilityVerdict.Aborted, verdict);
    }

    [Fact]
    public void Evaluator_ReportsFailed_WhenAnyEvidenceIsAFault()
    {
        var run = new ProbeRun(true, false,
        [
            ScriptedProbe.Sample(),
            ScriptedProbe.Sample(new ProbeEvidence.WheaErrors(1, "APEI")),
        ]);

        Assert.Equal(StabilityVerdict.FailedUnstable, StabilityEvaluator.Evaluate(run, StabilityThresholds.Default));
    }

    [Fact]
    public void Evaluator_DoesNotFailOnUnavailableSensor_ByDefault()
    {
        var run = new ProbeRun(true, false,
            [ScriptedProbe.Sample(new ProbeEvidence.SensorUnavailable("vrm-temp", "wmi-not-exposed"))]);

        // No poder leer un sensor no es estar inestable. Tampoco se maquilla como aprobado:
        // el veredicto es Passed pero la evidencia sigue diciendo que no se pudo medir.
        var verdict = StabilityEvaluator.Evaluate(run, StabilityThresholds.Default);

        Assert.Equal(StabilityVerdict.Passed, verdict);
        Assert.Single(StabilityEvaluator.UnavailableSensors(run));
    }

    [Fact]
    public void Evaluator_TreatsClockStallAsFault_AboveThreshold()
    {
        var thresholds = StabilityThresholds.Default;

        var under = new ProbeRun(true, false, [ScriptedProbe.Sample(new ProbeEvidence.ClockStall(5000, 4900, 0.02))]);
        var over = new ProbeRun(true, false, [ScriptedProbe.Sample(new ProbeEvidence.ClockStall(5000, 4000, 0.20))]);

        Assert.Equal(StabilityVerdict.Passed, StabilityEvaluator.Evaluate(under, thresholds));
        Assert.Equal(StabilityVerdict.FailedUnstable, StabilityEvaluator.Evaluate(over, thresholds));
    }

    [Fact]
    public void Evaluator_TreatsThermalLimitAsFault_ByDefault()
    {
        // El techo termico lo pone el firmware. Que CA-O lo detecte reaching-limit significa
        // que el valor propuesto es demasiado agresivo, y eso es un fallo del ajuste.
        var run = new ProbeRun(true, false, [ScriptedProbe.Sample(new ProbeEvidence.ThermalLimitReached("gpu-core", 83, 83))]);

        Assert.Equal(StabilityVerdict.FailedUnstable, StabilityEvaluator.Evaluate(run, StabilityThresholds.Default));
    }
}

/// <summary>El catalogo es datos puros y tiene que pasar su propio validador.</summary>
public sealed class OverclockCatalogTests
{
    private static IReadOnlyList<OverclockKnob> DefaultKnobs() =>
        OverclockCatalog.NvidiaGpuKnobs
            .Concat(OverclockCatalog.CpuPolicyKnobs)
            .Concat(OverclockCatalog.MemoryKnobs)
            .ToArray();

    [Fact]
    public void Catalog_Validate_AcceptsTheDefaultCatalog()
    {
        var validation = CatalogValidator.Validate(DefaultKnobs());

        Assert.True(validation.IsValid, string.Join("; ", validation.Violations.Select(v => $"{v.Rule}:{v.MessageKey}")));
    }

    [Fact]
    public void Catalog_Validate_RejectsCircularDependencies()
    {
        var knobs = new[]
        {
            OverclockTestKnobs.Scalar("a", dependsOn: [new KnobDependency("b", DependencyKind.Requires)]),
            OverclockTestKnobs.Scalar("b", dependsOn: [new KnobDependency("a", DependencyKind.Requires)]),
        };

        var validation = CatalogValidator.Validate(knobs);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Violations, v => v.Rule == "circular-dependency");
    }

    [Fact]
    public void Catalog_Validate_RejectsDanglingDependency()
    {
        var knobs = new[]
        {
            OverclockTestKnobs.Scalar("a", dependsOn: [new KnobDependency("nope", DependencyKind.Requires)]),
        };

        var validation = CatalogValidator.Validate(knobs);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Violations, v => v.Rule == "dangling-dependency");
    }

    [Fact]
    public void Catalog_Validate_RejectsDuplicateIds()
    {
        var knobs = new[] { OverclockTestKnobs.Scalar("a"), OverclockTestKnobs.Scalar("a") };

        var validation = CatalogValidator.Validate(knobs);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Violations, v => v.Rule == "duplicate-id");
    }

    [Fact]
    public void Catalog_Validate_RejectsInvertedRangeAndNonPositiveStep()
    {
        var knobs = new[]
        {
            OverclockTestKnobs.Scalar("a", min: 500, max: 100),
            OverclockTestKnobs.Scalar("b", step: 0),
        };

        var validation = CatalogValidator.Validate(knobs);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Violations, v => v.Rule == "inverted-range");
        Assert.Contains(validation.Violations, v => v.Rule == "non-positive-step");
    }

    [Fact]
    public void Knobs_NeverExposeUnknownEvidence_InDefaultProfile()
    {
        // Misma regla que el catalogo de 93 optimizaciones (README, "Evidencia honesta"):
        // nada aparece como Unknown. Si no se sabe, no se expone.
        var offenders = DefaultKnobs()
            .Where(k => k.Evidence == EvidenceLevel.Unknown)
            .Select(k => k.Id.Value)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Catalog_Validate_RejectsUnknownEvidence()
    {
        var validation = CatalogValidator.Validate([OverclockTestKnobs.Scalar("a", evidence: EvidenceLevel.Unknown)]);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Violations, v => v.Rule == "unknown-evidence");
    }

    [Fact]
    public void Catalog_MemoryKnobs_AreReadOnlyReporting()
    {
        // Memoria es solo lectura SMBIOS. CA-O no puede ni leer ni escribir XMP/EXPO en
        // modo usuario, asi que el catalogo no debe sugerir lo contrario.
        Assert.All(OverclockCatalog.MemoryKnobs, knob => Assert.Empty(knob.Presets));
        Assert.All(OverclockCatalog.MemoryKnobs, knob => Assert.Equal(RiskLevel.Safe, knob.Risk));
    }

    [Fact]
    public void Catalog_CpuKnobs_ArePowerPolicy_NotClockOverclock()
    {
        // Sin MSR no hay OC de CPU. Los knobs de CPU son politica de energia y su
        // evidencia es Official (Microsoft), nunca Vendor, porque no tocan el silicon.
        Assert.All(OverclockCatalog.CpuPolicyKnobs, knob => Assert.Equal(EvidenceLevel.Official, knob.Evidence));
        Assert.All(OverclockCatalog.CpuPolicyKnobs, knob => Assert.Equal(OverclockCatalog.IntelPolicyBackendId, knob.BackendId));
    }

    [Fact]
    public void Catalog_ScalarPresets_CarryScalars_NotIndexes()
    {
        // Trampa real del lenguaje: (Performance, 120) con literal int cae en
        // implicit operator KnobValue(int) => FromIndex, no FromScalar. Un preset
        // escalar guardado como indice es Scalar=0 y el tuning creeria que el techo
        // ya esta alcanzado. Los presets pueden superar el rango (el backend recorta
        // contra los limites del driver), pero nunca pueden ser indices.
        var knobs = OverclockCatalog.NvidiaGpuKnobs
            .Concat(OverclockCatalog.AmdGpuKnobs)
            .Concat(OverclockCatalog.CpuPolicyKnobs)
            .Concat(OverclockCatalog.MemoryKnobs);

        foreach (var knob in knobs.Where(k => k.Kind == KnobKind.Scalar))
        {
            foreach (var preset in knob.Presets)
            {
                Assert.Equal(0, preset.Value.Index);
            }
        }
    }
    [Fact]
    public void Catalog_KnobsRequiringReboot_AreSurfaced()
    {
        // Si un knob exige reinicio, la UI tiene que decirlo. Ninguno de los actuales lo
        // exige porque todos son de sesion, pero el contrato se fija aqui para que añadir
        // uno persistente no pueda olvidarse de la regla.
        var persisted = DefaultKnobs().Where(k => k.RequiresReboot).ToArray();

        Assert.Empty(persisted);
        Assert.All(DefaultKnobs(), knob => Assert.Equal(KnobScope.Session, knob.Scope));
    }

    [Fact]
    public void Catalog_EveryGpuKnob_DeclaresItsPowerDependency()
    {
        // Un offset de reloj sin headroom de potencia es rendimiento muerto con riesgo extra.
        var offsets = OverclockCatalog.NvidiaGpuKnobs
            .Where(k => k.Id.Value.EndsWith("Offset", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(offsets);
        Assert.All(offsets, knob =>
            Assert.Contains(knob.DependsOn, d => d.Kind == DependencyKind.PairsWith || d.Kind == DependencyKind.Requires));
    }
}

/// <summary>Invariantes de journal y de timeout compartidas con el resto de CA-O.</summary>
public sealed class OverclockTransactionContractTests
{
    private static readonly TransactionPhase[] NewOverclockPhases =
    [
        TransactionPhase.OverclockStarted,
        TransactionPhase.OverclockCheckpointTaken,
        TransactionPhase.OverclockStepApplied,
        TransactionPhase.OverclockStepRejected,
        TransactionPhase.OverclockTestPassed,
        TransactionPhase.OverclockTestFailed,
        TransactionPhase.OverclockInterrupted,
    ];

    [Fact]
    public void Transaction_OverclockPhases_DoNotChangeIsTerminal()
    {
        // IsTerminal es una lista explicita. Las fases nuevas quedan fuera solas, pero el
        // test lo fija para que anadir una terminal por descuido se detecte al instante.
        Assert.All(NewOverclockPhases, phase => Assert.False(TransactionEvent.IsTerminal(phase)));
    }

    [Fact]
    public void Transaction_ExistingTerminalPhases_AreUnchanged()
    {
        Assert.True(TransactionEvent.IsTerminal(TransactionPhase.Commit));
        Assert.True(TransactionEvent.IsTerminal(TransactionPhase.RolledBack));
        Assert.True(TransactionEvent.IsTerminal(TransactionPhase.Failed));
        Assert.True(TransactionEvent.IsTerminal(TransactionPhase.CancelledBeforeApply));
        Assert.True(TransactionEvent.IsTerminal(TransactionPhase.RecoveryCompleted));
    }

    [Fact]
    public void Transaction_CheckpointPhase_AgreesWithTransactionTerminality()
    {
        // Un checkpoint AutoReverted es terminal en su propio mundo pero NO cierra la
        // transaccion: la cierra quien la abrio.
        var reverted = OverclockTestKnobs.Checkpoint(Guid.NewGuid(), CheckpointPhase.AutoReverted);
        var closed = OverclockTestKnobs.Checkpoint(Guid.NewGuid(), CheckpointPhase.Closed);
        var open = OverclockTestKnobs.Checkpoint(Guid.NewGuid(), CheckpointPhase.Interrupted);

        Assert.True(reverted.IsTerminal);
        Assert.True(closed.IsTerminal);
        Assert.False(open.IsTerminal);
    }

    [Fact]
    public void Timeout_HeavyId_IsRegisteredNotCopied()
    {
        // El id pesado vive en TimeoutProfile. Copiar la lista en la UI ya fue un bug real
        // dos veces: si se copia, añadir un id aqui no llega a la UI.
        Assert.Contains("overclock-tune", TimeoutProfile.HeavyOptimizationIds);
        Assert.Equal(TimeSpan.FromMinutes(20), TimeoutProfile.DispatchHeavy);
        Assert.Equal(TimeSpan.FromMinutes(21), TimeoutProfile.ClientResponseHeavy);
    }

    [Fact]
    public void Paths_OverclockRunsDirectory_IsUnderProgramData()
    {
        // El journal de tuning sobrevive al proceso y al usuario. Debe vivir en
        // ProgramData\CA-O\overclock\runs, junto al resto del estado de CA-O.
        Assert.Contains("overclock", CaOPaths.OverclockRunsDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(CaOPaths.ProgramDataRoot, CaOPaths.OverclockRunsDirectory, StringComparison.OrdinalIgnoreCase);
    }
}