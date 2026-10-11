using System;

namespace CAO.Core.Overclock.Recovery;

public enum RecoveryAction
{
    /// <summary>Nada que hacer: el arranque fue limpio o el run ya estaba cerrado.</summary>
    None,

    /// <summary>
    /// Avisar al usuario y dejar que decida. Primer arranque sucio consecutivo: un solo
    /// cuelgue no demuestra que el valor sea inestable, y revertir sin más dejaría al
    /// usuario con un ajuste retirado que él no pidió retirar.
    /// </summary>
    NotifyUser,

    /// <summary>
    /// Revertir a LastKnownGood sin intervención. Solo al segundo arranque sucio
    /// consecutivo: dos cuelgues en el mismo punto sí son evidencia de que el valor
    /// concreto es el problema.
    /// </summary>
    AutoRevert,
}

public sealed record RecoveryDecision(
    RecoveryAction Action,
    CheckpointPhase ResultingPhase,
    int ConsecutiveUncleanBoots,
    string ReasonKey);

/// <summary>
/// La regla D4, aislada y pura. Vive separada del servicio para que los tres
/// escenarios (arranque limpio, primer cuelgue, segundo cuelgue) se verifiquen
/// sin arrancar la máquina ni montar un disco.
/// </summary>
public static class RecoveryPolicy
{
    public const string ReasonRunClosed = "recovery-run-closed";
    public const string ReasonBootClean = "recovery-boot-clean";
    public const string ReasonFirstUnclean = "recovery-first-unclean-boot";
    public const string ReasonRepeatedUnclean = "recovery-repeated-unclean-boot";
    public const string ReasonDriverChanged = "recovery-driver-changed";

    public static RecoveryDecision Decide(TuningCheckpoint checkpoint, bool lastBootWasClean, string? currentDriverVersion)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        if (checkpoint.IsTerminal)
        {
            return new RecoveryDecision(RecoveryAction.None, checkpoint.Phase, checkpoint.ConsecutiveUncleanBoots, ReasonRunClosed);
        }

        // Arranque limpio: el sistema bajó bien. No hay inestabilidad que revertir y el
        // contador se pone a cero, de modo que un cuelgue hoy y otro dentro de un mes no
        // sumen como "dos consecutivos".
        if (lastBootWasClean)
        {
            return new RecoveryDecision(RecoveryAction.None, checkpoint.Phase, 0, ReasonBootClean);
        }

        var next = checkpoint.ConsecutiveUncleanBoots + 1;

        if (next < 2)
        {
            return new RecoveryDecision(RecoveryAction.NotifyUser, CheckpointPhase.Interrupted, next, ReasonFirstUnclean);
        }

        // Guarda de versión de driver: si el driver cambió desde el paso guardado, el
        // valor "bueno" se escribió bajo otro intérprete de registros. Revertir a ciegas
        // podría dejar un reloj fuera de rango, así que se degrada a aviso.
        //
        // La unknowable es la misma situación que una versión distinta: no hay forma de
        // probar que el registro significa lo mismo, y el automatismo solo puede actuar
        // sobre lo que puede verificar. "No lo sé" nunca es "sí, es el mismo".
        var driverVerifiable =
            checkpoint.DriverVersion is not null &&
            currentDriverVersion is not null &&
            string.Equals(checkpoint.DriverVersion, currentDriverVersion, StringComparison.Ordinal);

        if (!driverVerifiable)
        {
            return new RecoveryDecision(RecoveryAction.NotifyUser, CheckpointPhase.Interrupted, next, ReasonDriverChanged);
        }

        return new RecoveryDecision(RecoveryAction.AutoRevert, CheckpointPhase.AutoReverted, next, ReasonRepeatedUnclean);
    }
}