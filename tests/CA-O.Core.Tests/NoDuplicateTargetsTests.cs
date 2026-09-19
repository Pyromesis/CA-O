using System.Reflection;
using CAO.Core.Catalog;
using CAO.Core.Optimizations;
using CAO.Core.Optimizations.Storage;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>Contrato anti-duplicados (spec §5.3): dos IDs distintos no pueden
/// reclamar el mismo directorio de limpieza ni la misma clave de registro,
/// salvo allowlist explícita con justificación. Impide futuros WU-dups en CI.</summary>
public sealed class NoDuplicateTargetsTests
{
    /// <summary>Pares declarados que comparten objetivo a propósito (vacío hoy).</summary>
    private static readonly HashSet<string> AllowlistedSharedTargets = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    [Fact]
    public void CleanupDirs_AreClaimedByASingleId()
    {
        var prop = typeof(TempFileCleanupOptimization).GetProperty("Targets",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(prop);
        var expand = typeof(TempFileCleanupOptimization).GetMethod("ExpandDir",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(expand);

        var byDir = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var opt in OptimizationCatalog.All.OfType<TempFileCleanupOptimization>())
        {
            var id = opt.Definition.Id;
            var targets = (System.Collections.IEnumerable)prop.GetValue(opt)!;
            foreach (var item in targets)
            {
                // DESVIACIÓN del verbatim del brief (nota del brief incorrecta):
                // (Directory, Pattern, OlderThanDays) es ValueTuple: los nombres
                // son solo metadata de compilación; en runtime son campos Item1..Item3.
                // GetProperty("Directory") devuelve null → NRE. Se lee Item1 (Directory).
                var dirField = item.GetType().GetField("Item1");
                Assert.NotNull(dirField);
                var raw = (string)dirField.GetValue(item)!;
                var expanded = ((string)expand.Invoke(null, [raw])!).TrimEnd('\\').ToUpperInvariant();
                var key = $"DIR::{expanded}";
                if (byDir.TryGetValue(key, out var other)
                    && !other.Equals(id, StringComparison.Ordinal)
                    && !AllowlistedSharedTargets.Contains($"{other}|{id}")
                    && !AllowlistedSharedTargets.Contains($"{id}|{other}"))
                {
                    Assert.Fail($"Directorio compartido sin declarar: '{expanded}' reclamado por '{other}' y '{id}'.");
                }
                byDir[key] = id;
            }
        }
    }

    [Fact]
    public void RegistryKeys_AreClaimedByASingleId()
    {
        var prop = typeof(RegistryOptimizationBase).GetProperty("Targets",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(prop);

        var byKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var opt in OptimizationCatalog.All.OfType<RegistryOptimizationBase>())
        {
            var id = opt.Definition.Id;
            var targets = (System.Collections.IEnumerable)prop.GetValue(opt)!;
            foreach (var item in targets)
            {
                var t = item.GetType();
                var hive = t.GetProperty("Hive")!.GetValue(item)!.ToString();
                var keyPath = (string)t.GetProperty("KeyPath")!.GetValue(item)!;
                var valueName = (string)t.GetProperty("ValueName")!.GetValue(item)!;
                var key = $"REG::{hive}\\{keyPath}\\{valueName}".ToUpperInvariant();
                if (byKey.TryGetValue(key, out var other)
                    && !other.Equals(id, StringComparison.Ordinal)
                    && !AllowlistedSharedTargets.Contains($"{other}|{id}")
                    && !AllowlistedSharedTargets.Contains($"{id}|{other}"))
                {
                    Assert.Fail($"Clave compartida sin declarar: '{key}' reclamada por '{other}' y '{id}'.");
                }
                byKey[key] = id;
            }
        }
    }
}
