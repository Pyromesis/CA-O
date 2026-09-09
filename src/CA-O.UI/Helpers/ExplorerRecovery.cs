using System.Diagnostics;

namespace CAO.UI.Helpers;

/// <summary>
/// Plan B local para el shell: corre en la UI (sesión interactiva del
/// usuario), así que no necesita al servicio privilegiado ni a la sesión 0.
/// Se usa como fallback cuando la vía servicio falla o está desactualizada.
/// </summary>
public static class ExplorerRecovery
{
    public static bool AnyExplorerRunning()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    if (process.SessionId > 0) return true;
                }
                catch { }
                finally { process.Dispose(); }
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Recupera el shell localmente: mata restos colgados (si se pide) y
    /// lanza explorer.exe en esta sesión. Devuelve mensaje listo para UI.
    /// </summary>
    public static async Task<string> RecoverLocallyAsync(bool killLeftovers, CancellationToken ct = default)
    {
        try
        {
            if (AnyExplorerRunning() && !killLeftovers)
                return "El Explorador ya está en ejecución en tu sesión.";
            if (killLeftovers)
            {
                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    try { process.Kill(); } catch { }
                    finally { process.Dispose(); }
                }
                for (var i = 0; i < 40 && AnyExplorerRunning(); i++)
                {
                    try { await Task.Delay(250, ct); } catch { break; }
                }
            }
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Process.Start(new ProcessStartInfo(explorer) { UseShellExecute = true });
            for (var i = 0; i < 80 && !AnyExplorerRunning(); i++)
            {
                try { await Task.Delay(250, ct); } catch { break; }
            }
            return AnyExplorerRunning()
                ? "✓ Explorador recuperado localmente: barra y escritorio de vuelta."
                : "No volvió el escritorio. Pulsa Ctrl+Mayús+Esc → Archivo → Ejecutar nueva tarea → escribe explorer.exe.";
        }
        catch (Exception ex)
        {
            return $"Recuperación local falló: {ex.Message}";
        }
    }
}
