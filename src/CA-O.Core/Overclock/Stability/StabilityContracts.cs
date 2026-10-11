using System.Collections.Generic;
using System.Linq;

namespace CAO.Core.Overclock.Stability;

/// <summary>Métricas que un <see cref="IStabilityProbe"/> puede observar durante un test.</summary>
public enum ProbeMetric
{
    WheaEvents,
    KernelBugcheck,
    EffectiveClockStall,
    GpuThermalLimit,
    Composite,
}

/// <summary>
/// Veredicto de un punto de prueba. Los nombres evitan colisionar con
/// <c>CAO.Shared.TransactionPhase.Failed</c>.
/// </summary>
public enum StabilityVerdict
{
    Pending,
    Passed,
    FailedUnstable,
    Aborted,
}

/// <summary>
/// Evidencia concreta observada por el probe. Es una unión discriminada: cada subtipo
/// corresponde a una métrica y NUNCA se colapsa a un score numérico único, porque un
/// score escondería qué señal fiscalizó exactamente el veredicto.
/// </summary>
public abstract record ProbeEvidence
{
    protected ProbeEvidence()
    {
    }

    public abstract ProbeMetric Metric { get; }

    /// <summary>Errores WHEA (machine check) acumulados durante el test.</summary>
    public sealed record WheaErrors(int Count, string? ProviderName = null) : ProbeEvidence
    {
        public override ProbeMetric Metric => ProbeMetric.WheaEvents;

        /// <summary>Un WHEA registrado siempre cuenta como fallo: no hay "WHEA tolerables".</summary>
        public bool IsFault => Count > 0;
    }

    /// <summary>Bugcheck / pantalla azul. <paramref name="HardwareVerified"/> distingue causa verificada de sospecha.</summary>
    public sealed record KernelBugcheck(string BugcheckCode, bool HardwareVerified = false) : ProbeEvidence
    {
        public override ProbeMetric Metric => ProbeMetric.KernelBugcheck;

        public bool IsFault => true;
    }

    /// <summary>El reloj efectivo se quedó corto respecto al objetivo: señal de inestabilidad antes del cuelgue.</summary>
    public sealed record ClockStall(double TargetMhz, double EffectiveMhz, double StallRatio) : ProbeEvidence
    {
        public override ProbeMetric Metric => ProbeMetric.EffectiveClockStall;

        public bool IsFault => StallRatio > 0;
    }

    /// <summary>Se alcanzó el límite térmico declarado por el firmware.</summary>
    public sealed record ThermalLimitReached(string SensorName, double LimitCelsius, double PeakCelsius) : ProbeEvidence
    {
        public override ProbeMetric Metric => ProbeMetric.GpuThermalLimit;

        public bool IsFault => true;
    }

    /// <summary>
    /// Un sensor que no se pudo leer. Se registra para que la UI pueda decir "sin datos"
    /// en lugar de dar a entender que todo va bien.
    /// </summary>
    public sealed record SensorUnavailable(string SensorName, string ReasonKey) : ProbeEvidence
    {
        public override ProbeMetric Metric => ProbeMetric.Composite;

        /// <summary>La ausencia de telemetría NO es un fallo, pero tampoco se maquilla como aprobado.</summary>
        public bool IsFault => false;
    }
}

/// <summary>Request de una ventana de prueba.</summary>
public sealed record ProbeRequest(TimeSpan Duration, IReadOnlyList<ProbeMetric> Metrics)
{
    public static ProbeRequest Default(TimeSpan duration) =>
        new(duration, new[]
        {
            ProbeMetric.WheaEvents,
            ProbeMetric.KernelBugcheck,
            ProbeMetric.EffectiveClockStall,
        });
}

/// <summary>Muestra puntual recogida por el probe.</summary>
public sealed record ProbeSample(DateTimeOffset TakenAt, IReadOnlyList<ProbeEvidence> Evidence)
{
    public IEnumerable<ProbeEvidence> Faults => Evidence.Where(IsFault);

    public bool FaultDetected => Evidence.Any(IsFault);

    internal static bool IsFault(ProbeEvidence e) => e switch
    {
        ProbeEvidence.WheaErrors w => w.IsFault,
        ProbeEvidence.KernelBugcheck b => b.IsFault,
        ProbeEvidence.ClockStall c => c.IsFault,
        ProbeEvidence.ThermalLimitReached t => t.IsFault,
        _ => false,
    };
}

/// <summary>Resultado de una ventana de prueba completa.</summary>
public sealed record ProbeRun(
    bool Completed,
    bool Aborted,
    IReadOnlyList<ProbeSample> Samples,
    string? AbortReasonKey = null)
{
    public static ProbeRun NotStarted(string? reasonKey = null) => new(false, false, System.Array.Empty<ProbeSample>(), reasonKey);

    public static ProbeRun AbortedRun(string reasonKey) => new(false, true, System.Array.Empty<ProbeSample>(), reasonKey);
}

/// <summary>
/// Fuente de telemetría de estabilidad. Implementaciones reales leen WHEA, el código de
/// bugcheck del último arranque y los relojes efectivos; en tests se sustituye por un
/// probe guionizado, de modo que el motor es determinista sin hardware.
/// </summary>
public interface IStabilityProbe
{
    string Id { get; }

    /// <summary>
    /// Qué métricas cubre realmente en esta máquina. Lo que no cubra se declara
    /// <c>SensorUnavailable</c> en lugar de omitirse en silencio.
    /// </summary>
    IReadOnlyList<ProbeMetric> SupportedMetrics { get; }

    Task<ProbeRun> RunAsync(ProbeRequest request, CancellationToken cancellationToken);
}

/// <summary>Umbrales con los que se convierte evidencia en veredicto.</summary>
public sealed record StabilityThresholds(
    double MaxClockStallRatio = 0.15,
    bool ThermalLimitIsFault = true,
    bool SensorUnavailableIsFault = false)
{
    public static StabilityThresholds Default { get; } = new();
}

/// <summary>Un punto evaluado del barrido: qué se aplicó, durante cuánto y con qué veredicto.</summary>
public sealed record StabilityPoint(
    KnobId Knob,
    KnobValue Value,
    TimeSpan Duration,
    StabilityVerdict Verdict,
    IReadOnlyList<ProbeEvidence> Evidence);