using System.Globalization;
using System.Runtime.InteropServices;

namespace CAO.Privileged;

/// <summary>
/// System timer resolution held by the privileged service process.
/// NtSetTimerResolution is reference-counted per process: while the service
/// runs without releasing, the requested resolution stays system-wide.
/// </summary>
internal static class TimerResolution
{
    public const uint Default100Ns = 156250;
    public const uint Minimum100Ns = 5000;

    [DllImport("ntdll.dll")]
    private static extern int NtSetTimerResolution(uint desired, bool set, out uint current);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryTimerResolution(out uint max, out uint min, out uint current);

    public static bool TrySet(uint desired100Ns, out uint actual100Ns, out string error)
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
            if (NtSetTimerResolution(clamped, true, out actual100Ns) != 0)
            {
                error = "El sistema rechazó el cambio de timer resolution.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "No se pudo ajustar el timer: " + ex.Message.Trim();
            return false;
        }
    }

    public static string FormatMs(uint value100Ns) =>
        (value100Ns / 10000.0).ToString("0.###", CultureInfo.InvariantCulture) + " ms";
}
