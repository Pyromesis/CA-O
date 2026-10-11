using System;
using System.Text.Json.Serialization;

namespace CAO.Core.Overclock.Recovery;

/// <summary>
/// Fase del checkpoint. Las fases no terminales son las únicas que el recovery
/// considera: si un run terminó bien, su checkpoint deja de ser evidencia abierta.
/// </summary>
public enum CheckpointPhase
{
    Started,

    /// <summary>Un valor está aplicado y todavía no ha pasado la ventana de estabilidad.</summary>
    StepApplied,

    Interrupted,

    AutoReverted,

    /// <summary>Terminal. El run se cerró con commit o rollback.</summary>
    Closed,
}

/// <summary>
/// Estado durable de un run de tuning, escrito ANTES de cualquier cambio y en cada
/// paso. Existe para que un reinicio a mitad de un ajuste deje la máquina en un estado
/// conocido en vez de en uno que adivina el siguiente arranque.
/// </summary>
public sealed record TuningCheckpoint
{
    [JsonPropertyName("runId")]
    public Guid RunId { get; init; }

    /// <summary>Backend que puede revertir estos valores. Sin él el revert es imposible.</summary>
    [JsonPropertyName("backendId")]
    public string BackendId { get; init; } = string.Empty;

    [JsonPropertyName("knob")]
    public string Knob { get; init; } = string.Empty;

    [JsonPropertyName("component")]
    public string Component { get; init; } = string.Empty;

    /// <summary>Valor aplicado en el último paso conocido.</summary>
    [JsonPropertyName("applied")]
    public double AppliedValue { get; init; }

    /// <summary>Último valor que pasó la ventana de estabilidad. Es el punto de retorno.</summary>
    [JsonPropertyName("lastKnownGood")]
    public double LastKnownGoodValue { get; init; }

    [JsonPropertyName("stableCeiling")]
    public double? StableCeiling { get; init; }

    [JsonPropertyName("phase")]
    public CheckpointPhase Phase { get; init; }

    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>
    /// Versión del driver en el momento del paso. Si al recuperar no coincide, el
    /// revert automático se abstiene: otro driver puede interpretar el registro de
    /// reloj de forma distinta y el valor "bueno" ya no significa lo mismo.
    /// </summary>
    [JsonPropertyName("driverVersion")]
    public string? DriverVersion { get; init; }

    /// <summary>
    /// Arranques sucios consecutivos acumulados. Sobrevive al proceso porque vive en el
    /// fichero, no en memoria: es el contador que permite distinguir "se colgó una vez"
    /// de "se cuelga siempre aquí" y actuar solo en el segundo caso.
    /// </summary>
    [JsonPropertyName("uncleanBoots")]
    public int ConsecutiveUncleanBoots { get; init; }

        /// <summary>
    /// Arranque contra el que este run ya fue evaluado por el recovery. Sin este
    /// marcador, abrir la página dos veces dentro del mismo arranque contaría el mismo
    /// reinicio dos veces y el revert automático se dispararía en el primero, que es
    /// justo lo que D4 promete no hacer. Un cuelgue necesita dos arranques, no dos
    /// visitas a la página.
    /// </summary>
    [JsonPropertyName("evaluatedBootUtc")]
    public DateTimeOffset? LastEvaluatedBootUtc { get; init; }

    public bool IsTerminal => Phase is CheckpointPhase.Closed or CheckpointPhase.AutoReverted;

    public TuningCheckpoint Touch(CheckpointPhase phase, DateTimeOffset now) =>
        this with { Phase = phase, UpdatedAtUtc = now };
}