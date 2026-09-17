using System.Text.Json;

namespace CAO.UI.Helpers;

/// <summary>
/// Lee la versión del servicio instalado y la compara con la app.
/// Los fixes viven en EL SERVICIO: si está desactualizado (p. ej. reinicio
/// del explorador antiguo que dejaba sin escritorio), la UI debe pedir
/// reinstalar en vez de fallar en silencio con el binario viejo.
/// </summary>
public static class ServiceVersionProbe
{
    /// <summary>
    /// Sonda de 10 s como máximo (CancelAfter sobre token enlazado: manda la
    /// cancelación del llamante y el techo propio, lo primero que llegue).
    /// Corre fuera del hilo UI: no captura el contexto de sincronización.
    /// </summary>
    public static async Task<string?> FetchAsync(PrivilegedPipeClient pipe, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            var response = await pipe.GetServiceStatusAsync(cts.Token).ConfigureAwait(false);
            if (response is not { Accepted: true } || string.IsNullOrWhiteSpace(response.DetailJson))
                return null;
            using var document = JsonDocument.Parse(response.DetailJson);
            if (document.RootElement.TryGetProperty("serviceVersion", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            if (document.RootElement.TryGetProperty("ServiceVersion", out var v2) && v2.ValueKind == JsonValueKind.String)
                return v2.GetString();
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsStale(string? serviceVersion)
    {
        if (string.IsNullOrWhiteSpace(serviceVersion)) return false;
        var clean = serviceVersion.Trim().TrimStart('v', 'V');
        return !clean.Equals(AppUpdater.CurrentVersion, StringComparison.OrdinalIgnoreCase);
    }
}
