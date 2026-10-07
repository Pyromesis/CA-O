using CAO.Core.Engine;
using CAO.Core.Rollback;
using CAO.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CAO.Privileged;

/// <summary>
/// Registra al arrancar el estado de recuperación pendiente.
/// </summary>
/// <remarks>
/// CAO-BUG-2026-10-06 (F2): el <see cref="CrashRecoveryService"/> se registraba en el
/// contenedor pero nadie lo consultaba, así que un equipo con una transacción
/// pendiente se quedaba en silencio hasta que el usuario intentaba cualquier
/// cambio y recibía <c>CAO-TXN-004</c> sin explicación. Con este servicio el
/// detalle queda en el registro del servicio, que es donde un técnico mira
/// primero.
///
/// Deliberadamente NO revierte nada al arrancar: revertir es una mutación del
/// sistema y tiene que ser una decisión del usuario, no un efecto secundario de
/// un reinicio. La salida existe hoy como operación explícita
/// <c>RecoverTransaction</c> y como acción en la pantalla Restaurar.
/// </remarks>
internal sealed class RecoveryStartupReporter(
    IServiceProvider services,
    ILogger<RecoveryStartupReporter> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Se ejecuta fuera del arranque para no retrasar la apertura del pipe:
        // la resolución del motor y el barrido del journal tardan lo que tardan.
        return Task.Run(() => Report(cancellationToken), CancellationToken.None);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void Report(CancellationToken cancellationToken)
    {
        try
        {
            var recovery = services.GetService<CrashRecoveryService>();
            if (recovery is null)
            {
                logger.LogWarning("Arranque: no hay gestor de recuperación registrado; el estado pendiente no se ha verificado.");
                return;
            }

            // El motor solo se resuelve para que el detectLive del gestor tenga
            // algo que consultar; si su construcción falla, el scan sigue siendo
            // válido y todas las entradas se resolverán como Unknown.
            _ = services.GetService<OptimizationEngine>();

            // Un solo barrido: Scan() recorre el journal entero y decide cada
            // entrada, así que llamarlo dos veces solo para contar repetiría todo
            // el trabajo en el arranque.
            var scanned = recovery.Scan();
            var pending = scanned
                .Where(c => c.Decision is RecoveryDecision.RollbackRequired
                    or RecoveryDecision.RecoveryRequired
                    or RecoveryDecision.Corrupted)
                .ToList();

            if (pending.Count == 0)
            {
                logger.LogInformation(
                    "Arranque: sin recuperaciones pendientes ({Total} entradas incompletas revisadas).",
                    scanned.Count);
                return;
            }

            logger.LogWarning(
                "Arranque: {Total} operacion(es) pendiente(s) de recuperar. Apply y revert devolveran {Code} hasta cerrarlas. Transacciones: {Ids}",
                pending.Count,
                ErrorCodes.TxnRecoveryPending,
                string.Join(", ", pending.Select(p => $"{p.OptimizationId} ({p.TransactionId:D}, {p.Decision})")));

            foreach (var candidate in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogWarning(
                    "Recuperacion pendiente: transaccion={TransactionId} optimizacion={OptimizationId} ultimaFase={Phase} decision={Decision} snapshot={HasSnapshot} estadoEnVivo={LiveState}",
                    candidate.TransactionId,
                    candidate.OptimizationId,
                    candidate.LastPhase,
                    candidate.Decision,
                    candidate.HasSnapshot,
                    candidate.LiveState);
            }
        }
        catch (OperationCanceledException)
        {
            // El host esta cerrando; no hay nada que reportar.
        }
        catch (Exception ex)
        {
            // Un fallo al informar NUNCA debe impedir que el servicio arranque:
            // el pipe es lo imprescindible; este registro es diagnóstico.
            logger.LogError(ex, "No se pudo revisar el estado de recuperacion al arrancar.");
        }
    }
}