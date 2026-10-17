using System.Runtime.CompilerServices;
using CAO.Core.Optimizations.Power;

namespace CAO.Core.Tests;

/// <summary>
/// La auditoría del catálogo aplica de verdad los ajustes de energía con un
/// ejecutor falso; el índice previo se persiste en disco. Se redirige a una
/// carpeta temporal para que la suite nunca escriba en %ProgramData%\CA-O.
/// </summary>
internal static class TestDataIsolation
{
    [ModuleInitializer]
    internal static void Initialize() =>
        PowerAcSettingOptimization.PreviousIndexDirectory =
            Path.Combine(Path.GetTempPath(), "CA-O-Core-Tests", "power-previous-" + Environment.ProcessId);
}
