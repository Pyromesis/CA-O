using System.Text.Json;
using System.Text.Json.Serialization;
using CAO.Shared;

namespace CAO.Core.Engine;

/// <summary>
/// Persistent ledger of one-shot optimizations already executed on this
/// machine. One-shot actions (flag <see cref="OptimizationFlags.OneShot"/>)
/// have no observable post-apply state, so <c>Detect</c> can never report
/// them as applied: without this ledger every re-analysis would offer them
/// again as <c>Recommended</c>. The ledger survives app restarts (JSON under
/// <see cref="CaOPaths.ProgramDataRoot"/>); entries are added on successful
/// apply and removed on successful revert by
/// <see cref="Optimization.OptimizationEngine"/>.
/// </summary>
public static class OneShotLedger
{
    public const string FileName = "one-shot-ledger.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Serializa el read-modify-write dentro del proceso. Entre procesos
    /// distintos la ventana sigue existiendo (limitación documentada: el
    /// servicio y la UI no aplican el mismo one-shot a la vez por diseño).
    /// </summary>
    private static readonly object Gate = new();

    public sealed record Store(
        [property: JsonPropertyName("applied")] Dictionary<string, DateTime> AppliedUtc);

    public static string DefaultPath() => Path.Combine(CaOPaths.ProgramDataRoot, FileName);

    /// <summary>Ids already executed (case-insensitive). Empty when no ledger exists.</summary>
    public static IReadOnlySet<string> LoadAll(string? filePath = null)
    {
        lock (Gate)
        {
            return LoadAllCore(filePath);
        }
    }

    private static IReadOnlySet<string> LoadAllCore(string? filePath)
    {
        try
        {
            var path = filePath ?? DefaultPath();
            if (!File.Exists(path))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var store = JsonSerializer.Deserialize<Store>(File.ReadAllText(path));
            return new HashSet<string>(
                store?.AppliedUtc.Keys ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static bool Contains(string id, string? filePath = null) =>
        LoadAll(filePath).Contains(id);

    /// <summary>Records a successful apply (upsert with UTC timestamp).</summary>
    public static void Mark(string id, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        lock (Gate)
        {
            var path = filePath ?? DefaultPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var map = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(path))
                {
                    var existing = JsonSerializer.Deserialize<Store>(File.ReadAllText(path));
                    if (existing?.AppliedUtc is not null)
                    {
                        foreach (var kv in existing.AppliedUtc)
                        {
                            map[kv.Key] = kv.Value;
                        }
                    }
                }
            }
            catch
            {
                // Ledger corrupto: se aparta a .corrupt-* para diagnóstico en
                // vez de borrar historial ajeno en silencio.
                QuarantineCorrupt(path);
            }

            map[id] = DateTime.UtcNow;
            File.WriteAllText(path, JsonSerializer.Serialize(new Store(map), JsonOptions));
        }
    }

    /// <summary>Removes the entry on successful revert. No-op when absent.</summary>
    public static void Remove(string id, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        lock (Gate)
        {
            var path = filePath ?? DefaultPath();
            Dictionary<string, DateTime>? map = null;
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                var existing = JsonSerializer.Deserialize<Store>(File.ReadAllText(path));
                map = existing?.AppliedUtc is null
                    ? null
                    : new Dictionary<string, DateTime>(existing.AppliedUtc, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return;
            }

            if (map is null || !map.Remove(id))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new Store(map), JsonOptions));
        }
    }

    private static void QuarantineCorrupt(string path)
    {
        try
        {
            var backup = path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(path, backup);
        }
        catch
        {
            // Sin cuarentena posible: se reconstruye igualmente.
        }
    }
}
