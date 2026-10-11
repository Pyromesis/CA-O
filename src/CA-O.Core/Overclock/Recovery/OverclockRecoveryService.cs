using System;
using System.Collections.Generic;
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
/// Aplica D4 al arrancar. Invariante central del módulo: tras un cuelgue la máquina
/// vuelve estable sin intervención humana. No siempre se puede revertir (fichero
/// corrupto, driver cambiado), y en ese caso el run NO se cierra: queda abierto para
/// que la UI muestre los tres botones y el usuario decida.
/// </summary>
public sealed class OverclockRecoveryService
{
    /// <summary>No hay backend registrado, así que el revert automático es imposible.</summary>
    public const string ReasonBackendUnavailable = "recovery-backend-unavailable";

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

        foreach (var checkpoint in checkpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (checkpoint.IsTerminal)
            {
                continue;
            }

            var currentDriverVersion = await ReadDriverVersionAsync(checkpoint.BackendId, cancellationToken).ConfigureAwait(false);
            var knob = new KnobId(checkpoint.Knob);

            // El backend se comprueba ANTES de decidir. Si ya no hay con qué revertir, el
            // revert automático es imposible por construcción, y eso no es lo mismo que
            // "el driver ha cambiado": sin esta comprobación el usuario vería el motivo
            // equivocado y pensaría que un reinicio de driver lo arregla.
            if (!_resolver.TryResolve(checkpoint.BackendId, out _))
            {
                await _store.SaveAsync(
                    checkpoint with
                    {
                        Phase = CheckpointPhase.Interrupted,
                        ConsecutiveUncleanBoots = checkpoint.ConsecutiveUncleanBoots + 1,
                        UpdatedAtUtc = _time.GetUtcNow(),
                    },
                    cancellationToken).ConfigureAwait(false);

                await JournalAsync(TransactionPhase.OverclockInterrupted, ReasonBackendUnavailable, cancellationToken).ConfigureAwait(false);
                outcomes.Add(new RecoveryOutcome(
                    checkpoint.RunId, knob, RecoveryAction.NotifyUser, CheckpointPhase.Interrupted, ReasonBackendUnavailable, null, null));
                continue;
            }

            var decision = RecoveryPolicy.Decide(checkpoint, boot.LastBootWasClean, currentDriverVersion);

            switch (decision.Action)
            {
                case RecoveryAction.None:
                    // Aun así se persiste el contador reiniciado: el decremento a cero tiene
                    // que sobrevivir al reinicio o el segundo cuelgue nunca se detectaría.
                    if (decision.ConsecutiveUncleanBoots != checkpoint.ConsecutiveUncleanBoots)
                    {
                        await _store
                            .SaveAsync(checkpoint with { ConsecutiveUncleanBoots = decision.ConsecutiveUncleanBoots }, cancellationToken)
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
                        },
                        cancellationToken).ConfigureAwait(false);

                    await JournalAsync(TransactionPhase.OverclockInterrupted, decision.ReasonKey, cancellationToken).ConfigureAwait(false);
                    outcomes.Add(new RecoveryOutcome(checkpoint.RunId, knob, RecoveryAction.NotifyUser, decision.ResultingPhase, decision.ReasonKey, null, null));
                    continue;

                case RecoveryAction.AutoRevert:
                    outcomes.Add(await AutoRevertAsync(checkpoint, knob, decision, cancellationToken).ConfigureAwait(false));
                    continue;

                default:
                    throw new InvalidOperationException($"Unhandled recovery action {decision.Action}.");
            }
        }

        return outcomes;
    }

    private async Task<RecoveryOutcome> AutoRevertAsync(
        TuningCheckpoint checkpoint,
        KnobId knob,
        RecoveryDecision decision,
        CancellationToken cancellationToken)
    {
        if (!_resolver.TryResolve(checkpoint.BackendId, out var target) || target is null)
        {
            // Sin backend no hay con qué revertir. El run queda abierto a propósito.
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
                },
                cancellationToken)
            .ConfigureAwait(false);

        // Solo se registra la fase NO terminal. Cerrar la transacción con una fase
        // terminal desde aquí sería inventar una transacción que este servicio no abrió:
        // la capa que la abrió decide cómo se cierra, y así un revert fallido nunca puede
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