using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>
/// Base para reinicios de servicios Windows: solucionadores puros que no
/// cambian configuración. Respeta servicios deshabilitados a propósito.
/// </summary>
public abstract class RestartWindowsServiceOptimization : IOptimization
{
    protected abstract string ServiceName { get; }
    protected abstract string ServiceLabel { get; }
    public abstract OptimizationDefinition Definition { get; }

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null)
            return OperationResult.Fail("Gestor de servicios no disponible.", "no-services");
        try
        {
            if (string.Equals(context.Services.GetStartType(ServiceName), "Disabled", StringComparison.OrdinalIgnoreCase))
                return OperationResult.Fail($"{ServiceLabel}: el servicio está deshabilitado a propósito y no se toca.", "service-disabled");
        }
        catch { }
        if (!context.Services.Exists(ServiceName))
            return OperationResult.Fail($"{ServiceLabel}: servicio no encontrado en este equipo.", "service-not-found");
        try { await context.Services.StopAsync(ServiceName, ct); }
        catch { }
        try { await context.Services.StartAsync(ServiceName, ct); }
        catch (Exception ex)
        {
            return OperationResult.Fail($"No se pudo iniciar {ServiceLabel}.", ex.Message);
        }
        return OperationResult.Ok($"{ServiceLabel} reiniciado.");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("El reinicio no requiere reversión."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null)
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown, "Gestor de servicios no disponible."));
        return Task.FromResult(context.Services.Exists(ServiceName)
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, $"{ServiceLabel} presente tras el reinicio.")
            : VerificationResult.Unknown(OptimizationState.Unknown, $"Servicio {ServiceName} no encontrado."));
    }
}
