using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CAO.Privileged;

/// <summary>
/// System timer resolution held by the privileged service process.
/// NtSetTimerResolution keeps one request per process: while the service
/// runs without releasing, the requested resolution stays in force. Since
/// Windows 10 2004 that request only reaches other processes (games) when
/// GlobalTimerResolutionRequests=1 (optimización enable-global-timer-resolution).
/// The chosen value is persisted so a reboot or service restart re-applies
/// it; restoring the default releases the request and forgets it.
/// </summary>
internal static class TimerResolution
{
    public const uint Default100Ns = 156250;
    public const uint Minimum100Ns = 5000;

    private static readonly object Gate = new();
    private static bool _held;

    internal static string PersistPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "CA-O", "timer-resolution.txt");

    [DllImport("ntdll.dll")]
    private static extern int NtSetTimerResolution(uint desired, bool set, out uint current);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryTimerResolution(out uint max, out uint min, out uint current);

    /// <summary>
    /// Fija la resolución pedida, o la libera si es la predeterminada. Persiste
    /// la elección solo cuando el sistema la acepta.
    /// </summary>
    public static bool TrySet(uint desired100Ns, out uint actual100Ns, out string error)
    {
        if (desired100Ns >= Default100Ns)
        {
            Release(out actual100Ns);
            error = string.Empty;
            Forget();
            return true;
        }

        if (!TryHold(desired100Ns, out actual100Ns, out error)) return false;
        Persist(desired100Ns);
        return true;
    }

    /// <summary>Re-aplica la resolución guardada al arrancar el servicio. Nunca lanza.</summary>
    public static bool TryRestorePersisted(out uint requested100Ns, out uint actual100Ns, out string error)
    {
        requested100Ns = 0;
        actual100Ns = 0;
        error = string.Empty;
        try
        {
            if (!File.Exists(PersistPath)) return false;
            if (!uint.TryParse(File.ReadAllText(PersistPath).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out requested100Ns)
                || requested100Ns is < Minimum100Ns or >= Default100Ns)
            {
                error = "Valor guardado de timer inválido; se descarta.";
                Forget();
                return false;
            }
            return TryHold(requested100Ns, out actual100Ns, out error);
        }
        catch (Exception ex)
        {
            error = "No se pudo leer el timer guardado: " + ex.Message.Trim();
            return false;
        }
    }

    /// <summary>True si Windows aplica las peticiones de timer a todo el sistema (clave global activa).</summary>
    public static bool IsGlobalModeEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel");
            return key?.GetValue("GlobalTimerResolutionRequests") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryHold(uint desired100Ns, out uint actual100Ns, out string error)
    {
        actual100Ns = 0;
        error = string.Empty;
        try
        {
            if (NtQueryTimerResolution(out _, out var min, out _) != 0 || min == 0)
            {
                min = Minimum100Ns;
            }
            var clamped = Math.Clamp(desired100Ns, min, Default100Ns);
            lock (Gate)
            {
                if (NtSetTimerResolution(clamped, true, out actual100Ns) != 0)
                {
                    error = "El sistema rechazó el cambio de timer resolution.";
                    return false;
                }
                _held = true;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "No se pudo ajustar el timer: " + ex.Message.Trim();
            return false;
        }
    }

    private static void Release(out uint actual100Ns)
    {
        actual100Ns = Default100Ns;
        try
        {
            lock (Gate)
            {
                if (_held)
                {
                    // set=false retira la petición de este proceso; fijar 15,625 ms
                    // con set=true la dejaba registrada para siempre.
                    if (NtSetTimerResolution(Default100Ns, false, out actual100Ns) == 0)
                    {
                        _held = false;
                    }
                }
                else if (NtQueryTimerResolution(out _, out _, out var current) == 0)
                {
                    actual100Ns = current;
                }
            }
        }
        catch
        {
            // Liberar es best-effort: el valor real se reporta con lo que haya.
        }
    }

    private static void Persist(uint value100Ns)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PersistPath)!);
            File.WriteAllText(PersistPath, value100Ns.ToString(CultureInfo.InvariantCulture));
        }
        catch
        {
            // Sin persistencia el timer sigue activo hasta el próximo reinicio.
        }
    }

    private static void Forget()
    {
        try { File.Delete(PersistPath); } catch { }
    }

    public static string FormatMs(uint value100Ns) =>
        (value100Ns / 10000.0).ToString("0.###", CultureInfo.InvariantCulture) + " ms";
}
