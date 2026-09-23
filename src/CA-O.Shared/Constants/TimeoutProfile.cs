namespace CAO.Shared;

/// <summary>
/// Fuente única de verdad para los techos de timeout del camino
/// UI → pipe → servicio → gateway de comandos.
/// Antes estos valores vivían como literales triplicados en
/// PrivilegedPipeService (servicio), SystemCommandGateway (gateway) y
/// PrivilegedPipeClient (UI): si divergían, el gateway podía matar
/// (TryKill) un DISM/defrag a mitad de ejecución y corromper el almacén
/// de componentes. Cualquier cambio de techo se hace aquí.
/// </summary>
public static class TimeoutProfile
{
    /// <summary>Techo de lectura de una petición en el pipe (15 s).</summary>
    public static readonly TimeSpan RequestRead = TimeSpan.FromSeconds(15);

    /// <summary>Techo de despacho de una operación normal en el servicio (60 s).</summary>
    public static readonly TimeSpan DispatchDefault = TimeSpan.FromSeconds(60);

    /// <summary>Techo de despacho de operaciones pesadas DISM/defrag (20 min).</summary>
    public static readonly TimeSpan DispatchHeavy = TimeSpan.FromMinutes(20);

    /// <summary>Techo de espera de respuesta en el cliente UI para operaciones normales (90 s).</summary>
    public static readonly TimeSpan ClientResponseDefault = TimeSpan.FromSeconds(90);

    /// <summary>Techo de espera de respuesta en el cliente UI para operaciones pesadas (21 min: 20 del servicio + 1 de margen).</summary>
    public static readonly TimeSpan ClientResponseHeavy = TimeSpan.FromMinutes(21);

    /// <summary>
    /// Optimizaciones cuyo Apply/Revert despacha hasta <see cref="DispatchHeavy"/>
    /// en el servicio. Compartido por servicio, gateway y cliente UI.
    /// </summary>
    public static readonly HashSet<string> HeavyOptimizationIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows-component-store-cleanup",
        "windows-component-store-resetbase",
        "optimize-system-drive",
        "retrim-system-ssd",
        "defragment-hdd-only",
        "disk-cleanup-system-files",
        "cleanup-windows-update-cache",
        "reset-network-stack-repair",
        "repair-windows-update",
    };
}
