using CAO.Core.Abstractions;

namespace CAO.Core.Rollback;

/// <summary>Rollback fidelity verdict (P0-6).</summary>
public enum SnapshotMatchLevel
{
    ExactMatch,
    Equivalent,
    Mismatch,
    Unknown,
}

public static class SnapshotComparison
{
    /// <summary>
    /// Compares a fresh post-rollback capture against the original snapshot:
    /// entry-by-entry existence + kind + value, both directions, PLUS service
    /// start types, PLUS stable RawNotes. Only an ExactMatch verifies a
    /// rollback; Unknown never passes. (Antes solo se miraba Registry: un
    /// cambio de servicio o powercfg revertido a medias daba falso
    /// ExactMatch y borraba el snapshot. RawNotes volátiles — marcas
    /// temporales y contadores — se excluyen; el resto sí es estado.)
    /// </summary>
    public static SnapshotMatchLevel Compare(OptimizationSnapshot original, OptimizationSnapshot fresh)
    {
        if (original.Registry.Count != fresh.Registry.Count ||
            !original.ServiceStartTypes.SequenceEqual(fresh.ServiceStartTypes, StringComparer.Ordinal) ||
            !StableNotesEqual(original.RawNotes, fresh.RawNotes))
        {
            return SnapshotMatchLevel.Mismatch;
        }

        var level = SnapshotMatchLevel.ExactMatch;
        foreach (var originalEntry in original.Registry)
        {
            var freshEntry = fresh.Registry.FirstOrDefault(entry =>
                string.Equals(entry.Hive, originalEntry.Hive, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.KeyPath, originalEntry.KeyPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.ValueName, originalEntry.ValueName, StringComparison.Ordinal));

            if (freshEntry is null)
            {
                return SnapshotMatchLevel.Mismatch;
            }

            if (!originalEntry.SemanticallyEquals(freshEntry))
            {
                return SnapshotMatchLevel.Mismatch;
            }

            if (originalEntry.Kind == RegistryValueKind2.None && freshEntry.Kind == RegistryValueKind2.None)
            {
                // El valor no existía antes ni después: revert perfecto
                // (borrar lo creado). Cuenta como coincidencia exacta.
                continue;
            }

            if (originalEntry.Kind == RegistryValueKind2.None || freshEntry.Kind == RegistryValueKind2.None)
            {
                level = level == SnapshotMatchLevel.ExactMatch ? SnapshotMatchLevel.Unknown : level;
            }
            else if (originalEntry.Kind != freshEntry.Kind && level == SnapshotMatchLevel.ExactMatch)
            {
                level = SnapshotMatchLevel.Equivalent;
            }
        }

        return level;
    }

    /// <summary>
    /// Notas que son marcas temporales o contadores de ejecución, no estado
    /// del sistema: se excluyen de la comparación exacta.
    /// </summary>
    private static readonly HashSet<string> VolatileNotePrefixes = new(StringComparer.Ordinal)
    {
        "restore-point-requested=",
        "expired-sessions=",
        "deleted-snapshots=",
        "scanned-folders=",
        "pending-files=",
        "deleted-files=",
        "reclaimed-bytes=",
    };

    private static bool StableNotesEqual(IReadOnlyList<string> original, IReadOnlyList<string> fresh)
    {
        static IEnumerable<string> Stable(IReadOnlyList<string> notes)
        {
            foreach (var note in notes)
            {
                var isVolatile = false;
                foreach (var prefix in VolatileNotePrefixes)
                {
                    if (note.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        isVolatile = true;
                        break;
                    }
                }
                if (!isVolatile) yield return note;
            }
        }
        return Stable(original).OrderBy(n => n, StringComparer.Ordinal)
            .SequenceEqual(Stable(fresh).OrderBy(n => n, StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
