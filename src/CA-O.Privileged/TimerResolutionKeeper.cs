using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CAO.Privileged;

/// <summary>
/// Al arrancar el servicio (tras reiniciar el equipo) re-aplica el timer
/// resolution que el usuario eligió en Limpieza. Antes se perdía en cada
/// reinicio y la página seguía mostrándolo como aplicado.
/// </summary>
internal sealed class TimerResolutionKeeper(ILogger<TimerResolutionKeeper> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (TimerResolution.TryRestorePersisted(out var requested, out var actual, out var error))
        {
            logger.LogInformation(
                "Timer resolution restaurado a {Actual} (pedido {Requested}); modo global {Global}.",
                TimerResolution.FormatMs(actual), TimerResolution.FormatMs(requested),
                TimerResolution.IsGlobalModeEnabled() ? "activo" : "inactivo");
        }
        else if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("Timer resolution guardado no restaurado: {Error}", error);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
