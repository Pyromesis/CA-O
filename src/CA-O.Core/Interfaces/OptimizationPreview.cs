using CAO.Shared;

namespace CAO.Core.Abstractions;

/// <summary>One concrete change a dry-run predicts (spec 41).</summary>
public sealed class PreviewLine
{
    public required string Kind { get; init; }        // Registry | Service | Power | Boot | Network

    public required string Target { get; init; }      // HKLM\...\Value | WSearch | ...

    public required string Before { get; init; }

    public required string After { get; init; }
}

/// <summary>
/// Dry-run result (FASE 40): exactly what WILL change, before/after per
/// resource, without mutating anything. Unknown values are rendered
/// honestly instead of guessed.
/// </summary>
public sealed record OptimizationPreview
{
    public required string OptimizationId { get; init; }

    public required IReadOnlyList<PreviewLine> Lines { get; init; }

    public required RiskLevel Risk { get; init; }

    public required SecurityImpact SecurityImpact { get; init; }

    public bool Reversible => !Flags.HasFlag(OptimizationFlags.NotReversible);

    public bool RequiresReboot => Flags.HasFlag(OptimizationFlags.RequiresReboot);

    public OptimizationFlags Flags { get; init; }
}

public static class PreviewExtensions
{
    /// <summary>Formats a registry snapshot entry the way previews show it.</summary>
    public static string FormatValue(this RegistrySnapshotEntry entry)
    {
        if (!entry.Existed)
        {
            return "(no existe)";
        }

        return entry.Value switch
        {
            byte[] bytes => $"binary[{bytes.Length}]",
            System.Collections.Generic.IEnumerable<string> multi => string.Join(" | ", multi),
            _ => entry.Value?.ToString() ?? string.Empty,
        };
    }
}
