using CAO.Shared;

namespace CAO.Core.Abstractions;

/// <summary>
/// Resolucion del codigo de error de una <see cref="OperationResult"/> que viaja
/// al cliente por el canal IPC.
/// </summary>
public static class OperationResultCodes
{
    /// <summary>
    /// Devuelve el codigo que el cliente debe recibir.
    /// CAO-BUG-2026-10-06: el motor (<c>OptimizationEngine</c>) ya distingue el motivo
    /// real en <see cref="OperationResult.Error"/> (<c>CAO-TXN-004</c> recuperacion
    /// pendiente, <c>CAO-SEC-020</c> modo de solo lectura, <c>CAO-GAME-001</c> juego
    /// bloqueado, <c>not-admin</c>, <c>no-restore-point</c>, conflicto de plan de
    /// energia, <c>CAO-TXN-007</c>...), pero el servicio privilegiado lo descartaba y
    /// emitia siempre <c>CAO-TXN-003</c>. La UI recibia el mismo codigo para cualquier
    /// fallo, sin forma de explicar al usuario por que habia fallado. Se propaga el
    /// codigo real y solo se recurre al generico cuando el motor no dio ninguno.
    /// </summary>
    public static string WireCode(string? engineError) =>
        string.IsNullOrWhiteSpace(engineError) ? ErrorCodes.TxnApplyFailed : engineError.Trim();
}