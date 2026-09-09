using System.Diagnostics;
using CAO.Core.Interop;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>
/// Operaciones compartidas sobre el shell (explorer.exe) para
/// restart-windows-explorer y recover-windows-explorer.
/// El servicio corre como SYSTEM en sesión 0: lanzar explorer.exe sin más
/// lo dejaría allí (invisible) y el escritorio del usuario seguiría muerto.
/// Por eso el relanzado siempre usa el token de la sesión interactiva.
/// </summary>
internal static class ExplorerShell
{
    public const string RecoveryHintEs =
        "Pulsa Ctrl+Mayús+Esc → Archivo → Ejecutar nueva tarea → escribe explorer.exe para recuperar el escritorio.";

    public static string ExplorerPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    /// <summary>¿Hay shell en una sesión interactiva (sesión &gt; 0)?</summary>
    public static bool AnyInteractiveExplorer()
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

    public static async Task<bool> WaitForExitAsync(CancellationToken ct)
    {
        for (var i = 0; i < 40 && AnyInteractiveExplorer(); i++)
        {
            try { await Task.Delay(250, ct); } catch { break; }
        }
        return !AnyInteractiveExplorer();
    }

    public static async Task<bool> WaitForStartAsync(CancellationToken ct)
    {
        for (var i = 0; i < 120 && !AnyInteractiveExplorer(); i++)
        {
            try { await Task.Delay(250, ct); } catch { break; }
        }
        return AnyInteractiveExplorer();
    }

    /// <summary>Relanza el shell en la sesión interactiva. Ok si ya estaba.</summary>
    public static async Task<(bool Ok, string Error)> EnsureInteractiveExplorerAsync(CancellationToken ct)
    {
        if (AnyInteractiveExplorer()) return (true, string.Empty);
        if (!InteractiveSessionLauncher.TryLaunch(ExplorerPath, null, hidden: false, out var launchError))
            return (false, $"No se pudo relanzar explorer.exe en tu sesión: {launchError} {RecoveryHintEs}");
        if (!await WaitForStartAsync(ct))
            return (false, $"Se lanzó explorer.exe pero el escritorio no volvió. {RecoveryHintEs}");
        return (true, string.Empty);
    }
}
