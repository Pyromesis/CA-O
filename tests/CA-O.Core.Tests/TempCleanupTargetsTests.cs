using System.Reflection;

using CAO.Core.Optimizations.Storage;

using CAO.Shared;

using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Contrato de los objetivos de la familia de limpieza de temporales.
/// CAO-BUG-2026-10-06: <c>cleanup-delivery-optimization-cache</c> declaraba
/// <c>OlderThanDays = 0</c>, que fija el corte en "ahora" y por tanto admite
/// tambien los chunks que Delivery Optimization esta descargando en ese
/// instante. Como esta limpieza (a diferencia de CleanupWindowsUpdateCache) no
/// detiene el servicio antes de borrar, podia eliminar contenido a medio
/// escribir. El margen minimo de la familia es 1 dia: lo que se esta
/// reescribiendo queda fuera del corte.
/// </summary>
public sealed class TempCleanupTargetsTests
{
    private static IEnumerable<(Type Type, string Directory, string Pattern, int OlderThanDays)> Targets()
    {
        var property = typeof(TempFileCleanupOptimization)
            .GetProperty("Targets", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "TempFileCleanupOptimization ya no expone la propiedad abstracta Targets.");

        var assembly = typeof(TempFileCleanupOptimization).Assembly;
        foreach (var type in assembly
                     .GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false }
                                 && typeof(TempFileCleanupOptimization).IsAssignableFrom(t))
                     .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            if (Activator.CreateInstance(type) is not object instance)
                continue;

            var targets = (IEnumerable<(string Directory, string Pattern, int OlderThanDays)>)
                property.GetValue(instance)!;
            foreach (var (directory, pattern, olderThanDays) in targets)
                yield return (type, directory, pattern, olderThanDays);
        }
    }

    [Fact]
    public void NoTarget_UsesAZeroDayCutoff()
    {
        var offenders = Targets()
            .Where(t => t.OlderThanDays < 1)
            .Select(t => $"{t.Type.Name}: {t.Directory} (patron '{t.Pattern}', {t.OlderThanDays} dias)")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Un corte de 0 dias borra tambien el contenido en transito:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// CAO-BUG-2026-10-06: los objetivos de la familia se filtran por existencia
    /// (<c>ProfileSubDirs</c> solo devuelve directorios reales), asi que en una
    /// maquina sin Outlook el conjunto queda vacio. Con la lista vacia
    /// <c>PendingFiles</c> no devuelve nada y <c>Detect</c> contestaba
    /// <c>AppliedByCao</c>: la transaccion respondia "Ya aplicado y verificado,
    /// no se puede volver a aplicar" y la accion "Limpiar cache Outlook" se
    /// quedaba como un no-op permanente que ademas parecia un exito. Cuando no
    /// hay nada que inspeccionar la respuesta honesta es "no aplicado".
    /// </summary>
    [Fact]
    public void Detect_NeverReportsApplied_WhenThereIsNothingToInspect()
    {
        var assembly = typeof(TempFileCleanupOptimization).Assembly;
        var types = assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(TempFileCleanupOptimization).IsAssignableFrom(t))
            .ToList();
        Assert.NotEmpty(types);

        var offenders = types
            .Where(t => !Targets().Any(x => x.Type == t))
            .Select(t => (Type: t, Optimization: (TempFileCleanupOptimization)Activator.CreateInstance(t)!))
            .Where(x => x.Optimization.Detect(new MemoryRegistry()) == OptimizationState.AppliedByCao)
            .Select(x => x.Type.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Sin objetivos no se ha inspeccionado nada; informar 'aplicado' es un exito falso:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}