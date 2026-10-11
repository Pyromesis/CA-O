using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CAO.Core.Overclock.Stability;
using CAO.Shared;

namespace CAO.Core.Overclock.Recovery;

/// <summary>
/// Cómo terminó el último arranque. Es una abstracción y no una llamada directa al registro
/// de Windows porque el comportamiento D4 tiene que poder probarse sin apagar el equipo.
/// </summary>
public interface IBootHealthProbe
{
    Task<BootHealthReport> GetAsync(CancellationToken cancellationToken);
}

public sealed record BootHealthReport(bool LastBootWasClean, DateTimeOffset? LastBootUtc, string? ReasonKey)
{
    public static BootHealthReport Clean { get; } = new(true, null, null);

    public static BootHealthReport Unclean(string reasonKey) => new(false, null, reasonKey);
}

public interface ITuningTargetResolver
{
    bool TryResolve(string backendId, out ITuningTarget? target);
}

public sealed record RecoveryOutcome(
    Guid RunId,
    KnobId Knob,
    RecoveryAction Action,
    CheckpointPhase Phase,
    string ReasonKey,
    bool? RevertSucceeded,
    string? FailureReasonKey)
{
    public bool NeedsUserDecision => Action == RecoveryAction.NotifyUser;
}

/// <summary>
/// Qué decide el usuario sobre un run que sigue abierto. Ninguna opción es la
/// "correcta" por defecto: revertir es lo conservador, mantener el valor es lo que
/// alguien que sabe que su máquina aguanta elige a propósito, y descartar solo tiene
/// sentido cuando el cuelgue no vino del overclock.
/// </summary>
public enum RecoveryChoice
{
    /// <summary>Volver al último valor conocido como estable y cerrar el run.</summary>
    Revert,

    /// <summary>Dejar el valor aplicado y cerrar el run como decisión consciente.</summary>
    Keep,

    /// <summary>Descartar el checkpoint. Se borra la evidencia; el hardware no cambia.</summary>
    Discard,
}

/// <summary>
/// Resultado de resolver un run abierto. <paramref name="Succeeded"/> dice si la opción
/// pedida pudo aplicarse y <paramref name="RunClosed"/> si el run queda cerrado. Son dos
/// cosas distintas a propósito: un revert que falló deja la máquina sin revertir, así que
/// cerrar el run ahí perdería la única evidencia que explica el cuelgue.
/// </summary>
public sealed record RecoveryResolveResult(
    Guid RunId,
    RecoveryChoice Choice,
    CheckpointPhase Phase,
    bool Succeeded,
    bool RunClosed,
    string? ReasonKey)
{
    public const string ReasonRunNotFound = "recovery-run-not-found";
    public const string ReasonAlreadyClosed = "recovery-run-already-closed";
    public const string ReasonUserReverted = "recovery-user-reverted";
    public const string ReasonUserKept = "recovery-user-kept";
    public const string ReasonUserDiscarded = "recovery-user-discarded";
    public const string ReasonRevertFailed = "recovery-user-revert-failed";
    public const string ReasonDiscardFailed = "recovery-user-discard-failed";

    public static RecoveryResolveResult NotFound(Guid runId, RecoveryChoice choice) =>
        new(runId, choice, CheckpointPhase.Interrupted, false, false, ReasonRunNotFound);
}

/// <summary>
/// Aplica D4 al arrancar. Invariante central del módulo: tras un cuelgue la máquina
/// vuelve estable sin intervención humana. No siempre se puede revertir (fichero
/// corrupto, driver cambiado), y en ese caso el run NO se cierra: queda abierto para
/// que la UI muestre los tres botones y el usuario decida.
/// </summary>
public sealed class OverclockRecoveryService
{
    /// <summary>No hay backend registrado, así que el revert automático es imposible.</summary>
    public const string ReasonBackendUnavailable = "recovery-backend-unavailable";

    /// <summary>
    /// Motivo de un run que este arranque ya evaluo. No es un fallo nuevo: es el mismo
    /// aviso reemitido, para que la pagina pueda mostrarlo sin contar el reinicio otra vez.
    /// </summary>
    public const string ReasonAlreadyDecided = "recovery-already-decided-this-boot";

    private readonly ITuningCheckpointStore _store;
    private readonly IBootHealthProbe _boot;
    private readonly ITuningTargetResolver _resolver;
    private readonly IOverclockJournal? _journal;
    private readonly TimeProvider _time;

    public OverclockRecoveryService(
        ITuningCheckpointStore store,
        IBootHealthProbe boot,
        ITuningTargetResolver resolver,
        IOverclockJournal? journal = null,
        TimeProvider? time = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _boot = boot ?? throw new ArgumentNullException(nameof(boot));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _journal = journal;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<RecoveryOutcome>> RecoverAllAsync(CancellationToken cancellationToken)
    {
        var boot = await _boot.GetAsync(cancellationToken).ConfigureAwait(false);
        var checkpoints = await _store.ListAsync(cancellationToken).ConfigureAwait(false);

        var outcomes = new List<RecoveryOutcome>();
        var bootUtc = boot.LastBootUtc;

        foreach (var checkpoint in checkpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (checkpoint.IsTerminal)
            {
                continue;
            }

            // Un run ya evaluado contra este arranque no se vuelve a contar. Sin esto,
            // abrir la pagina dos veces dentro del mismo arranque leeria el mismo reinicio
            // dos veces y el revert automatico saltaria en el primero, que es justo lo que
            // D4 promete no hacer: un reinicio avisa, solo el segundo consecutivo revierte.
            if (bootUtc is { } evaluatedBoot && checkpoint.LastEvaluatedBootUtc >= evaluatedBoot)
            {
                var reemitted = ReemitDecision(checkpoint);
                if (reemitted is not null)
                {
                    outcomes.Add(reemitted);
                }

                continue;
            }

            // Lo unico demostrable sin leer el Event Log es temporal: si el checkpoint se
            // escribio antes del arranque actual, la maquina arranco con el run abierto.
            // No se afirma que hubiera cuelgue porque no hay forma de probarlo desde aqui.
            var restartedDuringRun = bootUtc is { } lastBoot && checkpoint.UpdatedAtUtc < lastBoot.UtcDateTime;
            var lastBootWasClean = boot.LastBootWasClean && !restartedDuringRun;

            var currentDriverVersion = await ReadDriverVersionAsync(checkpoint.BackendId, cancellationToken).ConfigureAwait(false);
            var knob = new KnobId(checkpoint.Knob);

            // El backend se comprueba ANTES de decidir. Si ya no hay con que revertir, el
            // revert automatico es imposible por construccion, y eso no es lo mismo que
            // "el driver ha cambiado": sin esta comprobacion el usuario veria el motivo
            // equivocado y pensaria que un reinicio de driver lo arregla.
            if (!_resolver.TryResolve(checkpoint.BackendId, out _))
            {
                await _store.SaveAsync(
                    checkpoint with
                    {
                        Phase = CheckpointPhase.Interrupted,
                        ConsecutiveUncleanBoots = checkpoint.ConsecutiveUncleanBoots + 1,
                        UpdatedAtUtc = _time.GetUtcNow(),
                        LastEvaluatedBootUtc = bootUtc,
                    },
                    cancellationToken).ConfigureAwait(false);

                await JournalAsync(TransactionPhase.OverclockInterrupted, ReasonBackendUnavailable, cancellationToken).ConfigureAwait(false);
                outcomes.Add(new RecoveryOutcome(
                    checkpoint.RunId, knob, RecoveryAction.NotifyUser, CheckpointPhase.Interrupted, ReasonBackendUnavailable, null, null));
                continue;
            }

            var decision = RecoveryPolicy.Decide(checkpoint, lastBootWasClean, currentDriverVersion);

            switch (decision.Action)
            {
                case RecoveryAction.None:
                    // Aun asi se persiste el contador reiniciado: el decremento a cero tiene
                    // que sobrevivir al reinicio o el segundo cuelgue nunca se detectaria.
                    // Y se marca contra que arranque se evaluo, para que la proxima visita a
                    // la pagina dentro de este mismo arranque no lo cuente otra vez.
                    if (decision.ConsecutiveUncleanBoots != checkpoint.ConsecutiveUncleanBoots
                        || checkpoint.LastEvaluatedBootUtc != bootUtc)
                    {
                        await _store
                            .SaveAsync(
                                checkpoint with
                                {
                                    ConsecutiveUncleanBoots = decision.ConsecutiveUncleanBoots,
                                    LastEvaluatedBootUtc = bootUtc,
                                },
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    continue;

                case RecoveryAction.NotifyUser:
                    await _store.SaveAsync(
                        checkpoint with
                        {
                            Phase = decision.ResultingPhase,
                            ConsecutiveUncleanBoots = decision.ConsecutiveUncleanBoots,
                            UpdatedAtUtc = _time.GetUtcNow(),
                            LastEvaluatedBootUtc = bootUtc,
                        },
                        cancellationToken).ConfigureAwait(false);

                    await JournalAsync(TransactionPhase.OverclockInterrupted, decision.ReasonKey, cancellationToken).ConfigureAwait(false);
                    outcomes.Add(new RecoveryOutcome(checkpoint.RunId, knob, RecoveryAction.NotifyUser, decision.ResultingPhase, decision.ReasonKey, null, null));
                    continue;

                case RecoveryAction.AutoRevert:
                    outcomes.Add(await AutoRevertAsync(checkpoint, knob, decision, bootUtc, cancellationToken).ConfigureAwait(false));
                    continue;

                default:
                    throw new InvalidOperationException($"Unhandled recovery action {decision.Action}.");
            }
        }

        return outcomes;
    }

    /// <summary>
    /// Reemite la decision que este arranque ya tomo, sin volver a contarla. Un run que
    /// quedo abierto esperando al usuario sigue esperando: lo unico que cambia es el
    /// motivo, que dice que ya se evaluo, para que no se lea como un diagnostico nuevo.
    /// </summary>
    private static RecoveryOutcome? ReemitDecision(TuningCheckpoint checkpoint)
    {
        if (checkpoint.Phase != CheckpointPhase.Interrupted || checkpoint.ConsecutiveUncleanBoots <= 0)
        {
            return null;
        }

        return new RecoveryOutcome(
            checkpoint.RunId,
            new KnobId(checkpoint.Knob),
            RecoveryAction.NotifyUser,
            CheckpointPhase.Interrupted,
            ReasonAlreadyDecided,
            null,
            null);
    }

    /// <summary>
    /// Resuelve un run que sigue abierto con la decisión del usuario. Es la otra mitad de
    /// <see cref="RecoverAllAsync"/>: la parte automática deja el run abierto justamente
    /// cuando no puede decidir por sí sola, y aquí es donde se cierra.
    /// </summary>
    public async Task<RecoveryResolveResult> ResolveAsync(
        Guid runId,
        RecoveryChoice choice,
        CancellationToken cancellationToken = default)
    {
        var checkpoints = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
        var checkpoint = checkpoints.FirstOrDefault(c => c.RunId == runId);

        if (checkpoint is null)
        {
            return RecoveryResolveResult.NotFound(runId, choice);
        }

        if (checkpoint.IsTerminal)
        {
            // Ya está cerrado. Resolver otra vez no es un error que haya que ocultar,
            // pero tampoco una operación que se pueda dar por buena: se dice que no
            // ocurrió, y el run queda como estaba.
            return new RecoveryResolveResult(
                runId, choice, checkpoint.Phase, false, true, RecoveryResolveResult.ReasonAlreadyClosed);
        }

        return choice switch
        {
            RecoveryChoice.Revert => await RevertAsync(checkpoint, cancellationToken).ConfigureAwait(false),
            RecoveryChoice.Keep => await CloseAsync(checkpoint, RecoveryChoice.Keep, RecoveryResolveResult.ReasonUserKept, cancellationToken).ConfigureAwait(false),
            RecoveryChoice.Discard => await DiscardAsync(checkpoint, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unhandled recovery choice {choice}."),
        };
    }

    private async Task<RecoveryResolveResult> RevertAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var knob = new KnobId(checkpoint.Knob);

        if (!_resolver.TryResolve(checkpoint.BackendId, out var target) || target is null)
        {
            // Sin backend no hay con qué revertir, y el run NO se cierra: cerrarlo aquí
            // perdería la evidencia del cuelgue sin haber devuelto la máquina a un
            // estado estable, que es justo lo contrario de lo que D4 promete.
            return new RecoveryResolveResult(
                checkpoint.RunId, RecoveryChoice.Revert, CheckpointPhase.Interrupted, false, false, ReasonBackendUnavailable);
        }

        var write = await target
            .RestoreValueAsync(knob, KnobValue.FromScalar(checkpoint.LastKnownGoodValue), cancellationToken)
            .ConfigureAwait(false);

        if (!write.Success)
        {
            await _store
                .SaveAsync(
                    checkpoint with
                    {
                        Phase = CheckpointPhase.Interrupted,
                        UpdatedAtUtc = _time.GetUtcNow(),
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            await JournalAsync(TransactionPhase.OverclockInterrupted, RecoveryResolveResult.ReasonRevertFailed, cancellationToken).ConfigureAwait(false);

            return new RecoveryResolveResult(
                checkpoint.RunId, RecoveryChoice.Revert, CheckpointPhase.Interrupted, false, false,
                write.FailureReasonKey ?? RecoveryResolveResult.ReasonRevertFailed);
        }

        await _store
            .SaveAsync(
                checkpoint with
                {
                    Phase = CheckpointPhase.AutoReverted,
                    AppliedValue = checkpoint.LastKnownGoodValue,
                    ConsecutiveUncleanBoots = 0,
                    UpdatedAtUtc = _time.GetUtcNow(),
                },
                cancellationToken)
            .ConfigureAwait(false);

        await JournalAsync(TransactionPhase.OverclockInterrupted, RecoveryResolveResult.ReasonUserReverted, cancellationToken).ConfigureAwait(false);

        return new RecoveryResolveResult(
            checkpoint.RunId, RecoveryChoice.Revert, CheckpointPhase.AutoReverted, true, true, RecoveryResolveResult.ReasonUserReverted);
    }

    private async Task<RecoveryResolveResult> CloseAsync(
        TuningCheckpoint checkpoint,
        RecoveryChoice choice,
        string reasonKey,
        CancellationToken cancellationToken)
    {
        await _store
            .SaveAsync(
                checkpoint with
                {
                    Phase = CheckpointPhase.Closed,
                    ConsecutiveUncleanBoots = 0,
                    UpdatedAtUtc = _time.GetUtcNow(),
                },
                cancellationToken)
            .ConfigureAwait(false);

        await JournalAsync(TransactionPhase.OverclockInterrupted, reasonKey, cancellationToken).ConfigureAwait(false);

        return new RecoveryResolveResult(checkpoint.RunId, choice, CheckpointPhase.Closed, true, true, reasonKey);
    }

    private async Task<RecoveryResolveResult> DiscardAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var deleted = await _store.DeleteAsync(checkpoint.RunId, cancellationToken).ConfigureAwait(false);

        if (!deleted)
        {
            // El checkpoint sigue en disco, así que el run sigue abierto y el recovery
            // volverá a ofrecerlo en el próximo arranque. Decir que se descartó cuando
            // el fichero sigue ahí sería un cierre que no ocurrió.
            return new RecoveryResolveResult(
                checkpoint.RunId, RecoveryChoice.Discard, CheckpointPhase.Interrupted, false, false, RecoveryResolveResult.ReasonDiscardFailed);
        }

        await JournalAsync(TransactionPhase.OverclockInterrupted, RecoveryResolveResult.ReasonUserDiscarded, cancellationToken).ConfigureAwait(false);

        return new RecoveryResolveResult(
            checkpoint.RunId, RecoveryChoice.Discard, CheckpointPhase.Closed, true, true, RecoveryResolveResult.ReasonUserDiscarded);
    }

    private async Task<RecoveryOutcome> AutoRevertAsync(
        TuningCheckpoint checkpoint,
        KnobId knob,
        RecoveryDecision decision,
        DateTimeOffset? bootUtc,
        CancellationToken cancellationToken)
    {
        if (!_resolver.TryResolve(checkpoint.BackendId, out var target) || target is null)
        {
            // Sin backend no hay con que revertir. El run queda abierto a proposito.
            return new RecoveryOutcome(
                checkpoint.RunId, knob, RecoveryAction.NotifyUser, CheckpointPhase.Interrupted,
                ReasonBackendUnavailable, null, null);
        }

        var write = await target
            .RestoreValueAsync(knob, KnobValue.FromScalar(checkpoint.LastKnownGoodValue), cancellationToken)
            .ConfigureAwait(false);

        if (!write.Success)
        {
            await _store
                .SaveAsync(
                    checkpoint with
                    {
                        Phase = CheckpointPhase.Interrupted,
                        ConsecutiveUncleanBoots = decision.ConsecutiveUncleanBoots,
                        UpdatedAtUtc = _time.GetUtcNow(),
                        LastEvaluatedBootUtc = bootUtc,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return new RecoveryOutcome(
                checkpoint.RunId, knob, RecoveryAction.NotifyUser, CheckpointPhase.Interrupted,
                decision.ReasonKey, false, write.FailureReasonKey);
        }

        await _store
            .SaveAsync(
                checkpoint with
                {
                    Phase = CheckpointPhase.AutoReverted,
                    AppliedValue = checkpoint.LastKnownGoodValue,
                    ConsecutiveUncleanBoots = 0,
                    UpdatedAtUtc = _time.GetUtcNow(),
                    LastEvaluatedBootUtc = bootUtc,
                },
                cancellationToken)
            .ConfigureAwait(false);

        // Solo se registra la fase NO terminal. Cerrar la transaccion con una fase
        // terminal desde aqui seria inventar una transaccion que este servicio no abrio:
        // la capa que la abrio decide como se cierra, y asi un revert fallido nunca puede
        // disfrazarse de rollback limpio.
        await JournalAsync(TransactionPhase.OverclockInterrupted, "recovery-auto-reverted", cancellationToken).ConfigureAwait(false);

        return new RecoveryOutcome(
            checkpoint.RunId, knob, RecoveryAction.AutoRevert, CheckpointPhase.AutoReverted,
            decision.ReasonKey, true, null);
    }

    private async ValueTask<string?> ReadDriverVersionAsync(string backendId, CancellationToken cancellationToken)
    {
        if (!_resolver.TryResolve(backendId, out var target) || target is null)
        {
            return null;
        }

        var snapshot = await target.CaptureAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.DriverVersion;
    }

    private ValueTask JournalAsync(TransactionPhase phase, string? detail, CancellationToken cancellationToken) =>
        _journal is null ? ValueTask.CompletedTask : _journal.WriteAsync(phase, detail, cancellationToken);
}