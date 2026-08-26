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
    /// entry-by-entry existence + kind + value, both directions. Only an
    /// ExactMatch verifies a rollback; Unknown never passes.
    /// </summary>
    public static SnapshotMatchLevel Compare(OptimizationSnapshot original, OptimizationSnapshot fresh)
    {
        if (original.Registry.Count != fresh.Registry.Count)
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
}
