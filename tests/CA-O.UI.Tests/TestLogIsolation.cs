using System.Runtime.CompilerServices;
using CAO.UI.Helpers;

namespace CAO.UI.Tests;

/// <summary>
/// La suite ejercita caminos de error a propósito (sondas que explotan, cadenas
/// de 50 excepciones anidadas) y cada uno acaba en CrashLog. Sin este
/// aislamiento todo eso se escribía en %LOCALAPPDATA%\CA-O\logs\cao-ui-crash.log
/// del usuario real: ~2 MB de ruido de tests que tapaban los crashes reales de
/// la app. Se fija una ruta temporal para todo el ensamblado antes del primer test.
/// </summary>
internal static class TestLogIsolation
{
    internal static readonly string CrashLogPath = Path.Combine(
        Path.GetTempPath(), "CA-O-UI-Tests", "cao-ui-crash-" + Environment.ProcessId + ".log");

    [ModuleInitializer]
    internal static void Initialize() => CrashLog.LogPathOverride = CrashLogPath;
}
