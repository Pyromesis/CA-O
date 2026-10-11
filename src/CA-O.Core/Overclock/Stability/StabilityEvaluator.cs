using System.Collections.Generic;
using System.Linq;

namespace CAO.Core.Overclock.Stability;

/// <summary>
/// Convierte la evidencia recogida en un veredicto. Deliberadamente NO produce un
/// score: un número único obligaría a ponderar WHEA contra thermally contra stall, y
/// esa ponderación sería inventada. Cada señal conserva su peso y su procedencia.
/// </summary>
public static class StabilityEvaluator
{
    public static StabilityVerdict Evaluate(ProbeRun run, StabilityThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(thresholds);

        // Abortar no es fallar. La máquina se quedó sin tiempo o el usuario cortó:
        // el punto queda interrumpido y se puede reanudar, nunca se declara inestable.
        if (run.Aborted)
        {
            return StabilityVerdict.Aborted;
        }

        if (Faults(run, thresholds).Any())
        {
            return StabilityVerdict.FailedUnstable;
        }

        // Sin muestras no hay veredicto. Declarar "Passed" sobre una ventana que nunca
        // corrió es exactamente el fallo Hypertune llama "approved".
        if (!run.Completed || run.Samples.Count == 0)
        {
            return StabilityVerdict.Pending;
        }

        return StabilityVerdict.Passed;
    }

    /// <summary>Evidencias que motivan el fallo, ya filtradas por los umbrales configurados.</summary>
    public static IReadOnlyList<ProbeEvidence> Faults(ProbeRun run, StabilityThresholds thresholds)
    {
        var faults = new List<ProbeEvidence>();
        foreach (var sample in run.Samples)
        {
            foreach (var e in sample.Evidence)
            {
                if (IsFault(e, thresholds))
                {
                    faults.Add(e);
                }
            }
        }

        return faults;
    }

    /// <summary>Unifícalo aquí para que ninguna otra ruta pueda reimplementar el criterio y discrepar.</summary>
    public static bool IsFault(ProbeEvidence evidence, StabilityThresholds thresholds) => evidence switch
    {
        ProbeEvidence.WheaErrors w => w.Count > 0,
        ProbeEvidence.KernelBugcheck => true,
        ProbeEvidence.ClockStall c => c.StallRatio > thresholds.MaxClockStallRatio,
        ProbeEvidence.ThermalLimitReached t => thresholds.ThermalLimitIsFault,
        ProbeEvidence.SensorUnavailable => thresholds.SensorUnavailableIsFault,
        _ => false,
    };

    /// <summary>Qué métricas se pidieron observar pero no estaban disponibles en esta máquina.</summary>
    public static IReadOnlyList<ProbeEvidence> UnavailableSensors(ProbeRun run) =>
        run.Samples
            .SelectMany(s => s.Evidence)
            .OfType<ProbeEvidence.SensorUnavailable>()
            .ToList();
}