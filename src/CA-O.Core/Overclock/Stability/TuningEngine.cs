using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CAO.Shared;

namespace CAO.Core.Overclock.Stability;

/// <summary>
/// Lo que el motor de tuning necesita para tocar una máquina, sin saber si detrás hay
/// NVAPI, XTU o un fake de test. Existe para que el motor sea verificable sin hardware.
/// </summary>
public interface ITuningTarget
{
    string BackendId { get; }

    ValueTask<UnitSnapshot> CaptureAsync(CancellationToken cancellationToken);

    ValueTask<KnobWriteResult> ApplyAsync(KnobId id, KnobValue value, CancellationToken cancellationToken);

    ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Reabre un valor concreto tras un reinicio. El recovery no tiene un snapshot en
    /// memoria (el proceso que lo capturó ya no existe), solo el escalar guardado, así
    /// que necesita esta vía además de la restauración completa.
    /// </summary>
    ValueTask<KnobWriteResult> RestoreValueAsync(KnobId id, KnobValue value, CancellationToken cancellationToken);
}

/// <summary>
/// Receptor de las fases de journal del tuning. Deliberadamente NO conoce el commit:
/// cerrar la transacción es responsabilidad de la capa que la abrió, para que un
/// tuning interrumpido nunca pueda dejar la transacción en un estado terminal falso.
/// </summary>
public interface IOverclockJournal
{
    ValueTask WriteAsync(TransactionPhase phase, string? detail, CancellationToken cancellationToken);
}

public enum TuningStatus
{
    /// <summary>Se alcanzó el techo propuesto por el usuario sin que fallara la estabilidad.</summary>
    CeilingReached,

    /// <summary>La estabilidad falló antes del techo: ese es el techo real.</summary>
    StabilityCeilingFound,

    /// <summary>Se abortó (tiempo, corte del usuario, reinicio). Se puede reanudar.</summary>
    Interrupted,

    /// <summary>El backend no pudo ni aplicar el primer valor. No se abrió nada.</summary>
    Rejected,

    /// <summary>
    /// La sonda no pudo dar veredicto (sin carga, sin telemetría). Un punto sin
    /// veredicto NO es un aprobado: se vuelve al último valor bueno y se cierra.
    /// </summary>
    Unverified,
}

public sealed record TuningRequest(
    Guid RunId,
    OverclockKnob Knob,
    ITuningTarget Target,
    IStabilityProbe Probe,
    double ProposedCeiling,
    double Step,
    TimeSpan StepDuration,
    StabilityThresholds Thresholds,
    IReadOnlyList<ProbeMetric> Metrics)
{
    public double StartingValue { get; init; }

    public double Ceiling { get; init; }
}

public sealed record TuningProgress(int PointIndex, int EstimatedPoints, KnobValue Value, StabilityVerdict Verdict);

public sealed record TuningRunResult(
    Guid RunId,
    TuningStatus Status,
    KnobId Knob,
    KnobValue AppliedValue,
    double? StableCeiling,
    IReadOnlyList<StabilityPoint> Points,
    string? ReasonKey,
    double? ResumeFromValue)
{
    public bool CanResume => Status == TuningStatus.Interrupted && ResumeFromValue is not null;
}

/// <summary>
/// Barrido de estabilidad con bisección. El motor elige los puntos; el usuario aprueba
/// el perfil. Ante <see cref="StabilityVerdict.FailedUnstable"/> revierte a LastKnownGood,
/// marca el techo y continúa informacionalmente; ante <see cref="StabilityVerdict.Aborted"/>
/// marca Interrupted y OFRECE REANUDAR sin cerrar la ejecución.
/// </summary>
public sealed class TuningEngine
{
    private readonly IOverclockJournal? _journal;

    public TuningEngine(IOverclockJournal? journal = null) => _journal = journal;

    public async Task<TuningRunResult> RunAsync(
        TuningRequest request,
        IProgress<TuningProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var knob = request.Knob;
        var points = new List<StabilityPoint>();
        var pointsEstimated = EstimatePoints(request.StartingValue, request.Ceiling, request.Step);

        await WriteAsync(TransactionPhase.OverclockStarted, $"run={request.RunId:D}", cancellationToken).ConfigureAwait(false);

        // LastKnownGood arranca siendo el estado actual de la máquina: el punto de retorno
        // es "donde ya estábamos", nunca "donde cree el usuario que estamos".
        var lastGoodValue = KnobValue.FromScalar(request.StartingValue);
        var lastGoodSnapshot = await request.Target
            .CaptureAsync(cancellationToken)
            .ConfigureAwait(false);

        var value = request.StartingValue;

        while (value <= request.Ceiling)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var proposed = KnobValue.FromScalar(value);

            var applied = await request.Target
                .ApplyAsync(knob.Id, proposed, cancellationToken)
                .ConfigureAwait(false);

            if (!applied.Success)
            {
                await WriteAsync(TransactionPhase.OverclockStepRejected, applied.FailureReasonKey, cancellationToken).ConfigureAwait(false);
                return Build(request, TuningStatus.Rejected, lastGoodValue, null, points, applied.FailureReasonKey, null);
            }

            // El driver recorta contra su propio techo. Si lo que de verdad recibió no
            // pasa del último valor bueno, seguir subiendo solo repetiría el mismo punto:
            // el techo del hardware está alcanzado y se dice con el valor real.
            var actual = applied.Applied?.Scalar ?? value;
            if (points.Count > 0 && actual <= lastGoodValue.Scalar + (request.Step * 1e-6))
            {
                return Build(request, TuningStatus.CeilingReached, lastGoodValue, lastGoodValue.Scalar, points, null, null);
            }

            proposed = KnobValue.FromScalar(actual);

            await WriteAsync(TransactionPhase.OverclockStepApplied, knob.Id.Value, cancellationToken).ConfigureAwait(false);
            await WriteAsync(TransactionPhase.OverclockCheckpointTaken, $"{knob.Id}={Format(proposed)}", cancellationToken).ConfigureAwait(false);

            var run = await request.Probe
                .RunAsync(new ProbeRequest(request.StepDuration, request.Metrics), cancellationToken)
                .ConfigureAwait(false);

            var verdict = StabilityEvaluator.Evaluate(run, request.Thresholds);
            var evidence = run.Samples.SelectMany(s => s.Evidence).ToList();
            points.Add(new StabilityPoint(knob.Id, proposed, request.StepDuration, verdict, evidence));
            progress?.Report(new TuningProgress(points.Count, pointsEstimated, proposed, verdict));

            if (verdict == StabilityVerdict.Aborted)
            {
                await WriteAsync(TransactionPhase.OverclockInterrupted, run.AbortReasonKey, cancellationToken).ConfigureAwait(false);

                // Se deja el valor EN EL AIRE, no se revierte: reviertiendo se perdería el
                // progreso y la máquina quedaría peor que antes de empezar. Quien decide
                // es el recovery service al arrancar, con el checkpoint delante.
                var resume = Math.Min(value, request.Ceiling);
                return Build(request, TuningStatus.Interrupted, proposed, null, points, run.AbortReasonKey, resume);
            }

            if (verdict == StabilityVerdict.FailedUnstable)
            {
                await WriteAsync(TransactionPhase.OverclockTestFailed, knob.Id.Value, cancellationToken).ConfigureAwait(false);

                // Contrato clave: la máquina NO se queda en el valor que acaba de fallar.
                await RestoreAsync(request, lastGoodSnapshot, cancellationToken).ConfigureAwait(false);
                await WriteAsync(TransactionPhase.OverclockStepRejected, "unstable-restored-last-known-good", cancellationToken).ConfigureAwait(false);

                var ceiling = Math.Max(request.StartingValue, value - request.Step);
                return Build(request, TuningStatus.StabilityCeilingFound, lastGoodValue, ceiling, points, null, null);
            }

            if (verdict == StabilityVerdict.Pending)
            {
                // Sin veredicto no hay aprobado: una ventana sin carga o sin telemetría
                // no demuestra nada. Se vuelve al último valor bueno y se cierra.
                await RestoreAsync(request, lastGoodSnapshot, cancellationToken).ConfigureAwait(false);
                var reason = run.AbortReasonKey ?? "overclock-tuning-no-verdict";
                await WriteAsync(TransactionPhase.OverclockStepRejected, reason, cancellationToken).ConfigureAwait(false);
                return Build(request, TuningStatus.Unverified, lastGoodValue, null, points, reason, null);
            }

            await WriteAsync(TransactionPhase.OverclockTestPassed, knob.Id.Value, cancellationToken).ConfigureAwait(false);

            lastGoodValue = proposed;
            lastGoodSnapshot = await request.Target
                .CaptureAsync(cancellationToken)
                .ConfigureAwait(false);

            value += request.Step;
        }

        return Build(request, TuningStatus.CeilingReached, lastGoodValue, request.Ceiling, points, null, null);
    }

    private async Task RestoreAsync(TuningRequest request, UnitSnapshot snapshot, CancellationToken cancellationToken)
    {
        var outcome = await request.Target.RestoreAsync(snapshot, cancellationToken).ConfigureAwait(false);

        // Un revert fallido es peor que un revert: significa que no sabemos en qué estado
        // está la máquina. Se propaga para que la transacción quede abierta y el recovery
        // service sea quien decida.
        if (!outcome.Success)
        {
            throw new TuningRestoreFailedException(snapshot.Component.ToString(), outcome.FailureReasonKey);
        }
    }

    private ValueTask WriteAsync(TransactionPhase phase, string? detail, CancellationToken cancellationToken) =>
        _journal is null ? ValueTask.CompletedTask : _journal.WriteAsync(phase, detail, cancellationToken);

    private static TuningRunResult Build(
        TuningRequest request,
        TuningStatus status,
        KnobValue applied,
        double? ceiling,
        IReadOnlyList<StabilityPoint> points,
        string? reasonKey,
        double? resumeFrom) =>
        new(request.RunId, status, request.Knob.Id, applied, ceiling, points, reasonKey, resumeFrom);

    private static int EstimatePoints(double start, double ceiling, double step)
    {
        if (step <= 0 || ceiling <= start)
        {
            return 1;
        }

        return (int)Math.Ceiling((ceiling - start) / step) + 1;
    }

    private static string Format(KnobValue v) =>
        v.Scalar.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>
/// El revert a LastKnownGood falló: la máquina puede estar en un estado que nadie ha
/// verificado. No se captura ni se silencia; interrumpe el tuning y deja la transacción
/// abierta para que el recovery service la cierre.
/// </summary>
public sealed class TuningRestoreFailedException : InvalidOperationException
{
    public TuningRestoreFailedException(string component, string? reasonKey)
        : base($"Restore to last known good failed for {component} (reasonKey: {reasonKey ?? "unspecified"}).")
        => ReasonKey = reasonKey;

    public string? ReasonKey { get; }
}