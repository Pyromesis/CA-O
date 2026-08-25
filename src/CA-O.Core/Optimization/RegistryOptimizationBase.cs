using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations;

/// <summary>
/// Shared plumbing for optimizations whose whole behavior is a set of
/// registry values: Detect reads them, Capture snapshots them, Apply writes
/// the target values, Revert restores the exact captured state (including
/// deleting values that did not exist before).
/// </summary>
public abstract class RegistryOptimizationBase : IOptimization
{
    public abstract OptimizationDefinition Definition { get; }

    protected sealed record ValueTarget(
        RegistryHive2 Hive,
        string KeyPath,
        string ValueName,
        object AppliedValue,
        RegistryValueKind2 Kind = RegistryValueKind2.DWord);

    protected abstract IReadOnlyList<ValueTarget> Targets { get; }

    public virtual OptimizationState Detect(IRegistryAccessor registry)
    {
        foreach (var target in Targets)
        {
            var current = registry.GetValue(target.Hive, target.KeyPath, target.ValueName);
            if (!Equals(Normalize(current), Normalize(target.AppliedValue)))
            {
                return OptimizationState.NotApplied;
            }
        }
        return OptimizationState.AppliedByCao;
    }

    public virtual OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        foreach (var target in Targets)
        {
            // Exact capture (FASE 8): real kind + unexpanded/uncoerced data.
            var existing = registry.GetValueRaw(target.Hive, target.KeyPath, target.ValueName, out var kind);
            snapshot.Registry.Add(new RegistrySnapshotEntry(
                target.Hive.ToString(), target.KeyPath, target.ValueName, existing,
                Existed: existing is not null)
            { Kind = existing is null ? RegistryValueKind2.None : kind });
        }
        return snapshot;
    }

    public abstract Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default);

    public virtual Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        foreach (var entry in snapshot.Registry)
        {
            var hive = Enum.Parse<RegistryHive2>(entry.Hive);
            if (entry.Existed && entry.Value is not null)
            {
                // EXACT restore (FASE 8/9): declared kind, no inference.
                context.Registry.SetValueRaw(hive, entry.KeyPath, entry.ValueName,
                    entry.Value, entry.Kind);
            }
            else
            {
                context.Registry.DeleteValue(hive, entry.KeyPath, entry.ValueName);
            }
        }
        return Task.FromResult(OperationResult.Ok("Estado anterior restaurado desde el snapshot."));
    }

    /// <summary>
    /// DRY RUN (FASE 41): per-value diff lines for every registry target —
    /// exact current value vs applied value, with the real kind name.
    /// Reads only; never writes.
    /// </summary>
    public virtual Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var definition = Definition;
        var lines = new List<PreviewLine>();

        foreach (var target in Targets)
        {
            var raw = registry.GetValueRaw(target.Hive, target.KeyPath, target.ValueName, out var kind);
            var before = new RegistrySnapshotEntry(
                target.Hive.ToString(), target.KeyPath, target.ValueName, raw,
                Existed: raw is not null) { Kind = kind }.FormatValue();

            var afterValue = target.AppliedValue;
            var after = afterValue switch
            {
                byte[] bytes => $"binary[{bytes.Length}]",
                string[] multi => string.Join(" | ", multi),
                _ => afterValue?.ToString() ?? string.Empty,
            };

            lines.Add(new PreviewLine
            {
                Kind = "Registry",
                Target = $"{target.Hive}\\{target.KeyPath}\\{target.ValueName} ({kind.ToWire()})",
                Before = before,
                After = after,
            });
        }

        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = definition.Id,
            Lines = lines,
            Risk = definition.Risk,
            SecurityImpact = definition.SecurityImpact,
            Flags = definition.Flags,
        });
    }

    /// <summary>Writes every target value. Called by typical ApplyAsync bodies.</summary>
    protected void WriteTargets(OptimizationContext context)
    {
        foreach (var target in Targets)
        {
            context.Registry.SetValue(target.Hive, target.KeyPath, target.ValueName, target.AppliedValue, target.Kind);
        }
    }

    private static object? Normalize(object? value) => value switch
    {
        int i => (long)i,
        uint u => (long)u,
        long l => l,
        string s => s,
        null => null,
        _ => value.ToString(),
    };
}
