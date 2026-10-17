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

    // Evidencia de sesión: el reinicio se ejecutó sin error en este proceso.
    // IServiceManager no expone estado running/stopped, así que sin esta
    // evidencia el Verify es Unknown honesto (nunca Passed por mera existencia).
    private bool _restartedOk;

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    /// <summary>
    /// Servicios a reiniciar, en orden. Por defecto solo <see cref="ServiceName"/>;
    /// los arreglos de varias piezas (Xbox) declaran la lista completa.
    /// </summary>
    protected virtual IReadOnlyList<string> ServiceNames => [ServiceName];

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null)
            return OperationResult.Fail("Gestor de servicios no disponible.", "no-services");
        if (ServiceNames.Count == 1)
            return await RestartOneAsync(context.Services, ServiceName, ct);

        var restarted = new List<string>();
        var skipped = new List<string>();
        foreach (var name in ServiceNames)
        {
            ct.ThrowIfCancellationRequested();
            var single = await RestartOneAsync(context.Services, name, ct);
            (single.Success ? restarted : skipped).Add(name);
        }
        if (restarted.Count == 0)
            return OperationResult.Fail($"{ServiceLabel}: ningún servicio se pudo reiniciar ({string.Join(", ", skipped)}).", "no-service-restarted");
        _restartedOk = true;
        return OperationResult.Ok($"{ServiceLabel}: reiniciados {string.Join(", ", restarted)}" +
            (skipped.Count > 0 ? $"; omitidos (no instalados o deshabilitados) {string.Join(", ", skipped)}." : "."));
    }

    private async Task<OperationResult> RestartOneAsync(IServiceManager services, string name, CancellationToken ct)
    {
        try
        {
            if (string.Equals(services.GetStartType(name), "Disabled", StringComparison.OrdinalIgnoreCase))
                return OperationResult.Fail($"{ServiceLabel}: el servicio está deshabilitado a propósito y no se toca.", "service-disabled");
        }
        catch { }
        if (!services.Exists(name))
            return OperationResult.Fail($"{ServiceLabel}: servicio no encontrado en este equipo.", "service-not-found");
        try { await services.StopAsync(name, ct); }
        catch { }
        try { await services.StartAsync(name, ct); }
        catch (Exception ex)
        {
            return OperationResult.Fail($"No se pudo iniciar {ServiceLabel}.", ex.Message);
        }
        _restartedOk = true;
        return OperationResult.Ok($"{ServiceLabel} reiniciado.");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("El reinicio no requiere reversión."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        // M2: IServiceManager no expone estado running/stopped, así que no se
        // puede afirmar Passed por mera existencia. Passed solo con evidencia
        // de sesión (el reinicio se ejecutó sin error en este proceso);
        // sin ella, Unknown honesto en vez de Passed ficticio.
        if (context.Services is null)
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown, "Gestor de servicios no disponible."));
        if (_restartedOk && ServiceNames.Any(context.Services.Exists))
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao, $"{ServiceLabel} reiniciado sin error en esta sesión."));
        return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
            $"Servicio {ServiceName}: reinicio no observado en esta sesión."));
    }
}
