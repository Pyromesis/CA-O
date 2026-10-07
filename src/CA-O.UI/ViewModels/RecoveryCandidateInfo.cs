namespace CAO.UI.ViewModels;

/// <summary>
/// Operacion pendiente de recuperacion tal y como la reporta
/// <c>CrashRecoveryService.Scan</c>.
/// CAO-BUG-2026-10-06 (N-1): la UI solo conservaba el id de optimizacion, y con el
/// servicio en modo recuperacion tanto <c>ApplyAsync</c> como <c>RevertAsync</c>
/// devuelven <c>CAO-TXN-004</c>. Sin el TransactionId no habia ninguna forma de
/// cerrar la transaccion pendiente, de modo que el equipo se quedaba bloqueado sin
/// salida. La decision se muestra porque determina si la operacion se puede
/// revertir desde el snapshot o solo se puede descartar.
/// </summary>
/// <param name="TransactionId">Identidad de la transaccion en el journal.</param>
/// <param name="OptimizationId">Optimizacion afectada.</param>
/// <param name="Decision">Decision de <c>Scan</c>; solo llegan aqui las bloqueantes.</param>
public sealed record RecoveryCandidateInfo(Guid TransactionId, string OptimizationId, string Decision);
