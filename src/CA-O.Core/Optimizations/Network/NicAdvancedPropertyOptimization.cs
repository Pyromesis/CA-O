using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// Base para ajustes reales por adaptador (propiedades avanzadas NDIS).
/// Enumera instancias físicas bajo la clase Net y solo toca las que YA
/// exponen la propiedad. Nunca inventa valores en adaptadores sin soporte.
/// </summary>
public abstract class NicAdvancedPropertyOptimization : IOptimization
{
    public abstract OptimizationDefinition Definition { get; }

    /// <summary>Nombres de propiedad NDIS a activar (p. ej. *TCPChecksumOffloadIPv4).</summary>
    protected abstract string[] PropertyNames { get; }

    /// <summary>Texto/valor de "activado" (p. ej. "1").</summary>
    protected abstract string EnabledText { get; }

    /// <summary>Texto/valor de "desactivado" para la reversión (p. ej. "0").</summary>
    protected abstract string DisabledText { get; }

    private const string NetClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    private static readonly string[] VirtualKeywords =
        ["virtual", "vpn", "hyper-v", "miniport", "bluetooth", "wan ", "loopback", "tunnel", "teredo", "isatap"];

    protected IReadOnlyList<(string InstanceKey, string Property)> TargetsOf(IRegistryAccessor registry)
    {
        var found = new List<(string, string)>();
        IReadOnlyList<string> instances;
        try { instances = registry.GetSubKeyNames(RegistryHive2.LocalMachine, NetClassKey); }
        catch { return found; }
        foreach (var instance in instances)
        {
            var instanceKey = $@"{NetClassKey}\{instance}";
            if (!IsPhysicalAdapter(registry, instanceKey)) continue;
            foreach (var property in PropertyNames)
            {
                var existing = registry.GetValue(RegistryHive2.LocalMachine, instanceKey, property);
                if (existing is not null) found.Add((instanceKey, property));
            }
        }
        return found;
    }

    private static bool IsPhysicalAdapter(IRegistryAccessor registry, string instanceKey)
    {
        var desc = registry.GetValue(RegistryHive2.LocalMachine, instanceKey, "DriverDesc") as string;
        if (string.IsNullOrWhiteSpace(desc)) return false;
        return !VirtualKeywords.Any(k => desc.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEnabled(object? value, string enabledText) => value switch
    {
        int i => i.ToString() == enabledText,
        uint u => u.ToString() == enabledText,
        long l => l.ToString() == enabledText,
        string s => s.Trim().Equals(enabledText, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private static object ToKindValue(object current, string text, out bool supported)
    {
        supported = true;
        return current switch
        {
            int => int.TryParse(text, out var i) ? i : 0,
            uint => uint.TryParse(text, out var u) ? u : 0u,
            long => long.TryParse(text, out var l) ? l : 0L,
            string => text,
            _ => ApplyUnsupported(out supported),
        };
    }

    private static object ApplyUnsupported(out bool supported)
    {
        supported = false;
        return 0;
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var targets = TargetsOf(registry);
        if (targets.Count == 0) return OptimizationState.Unknown;
        foreach (var (instanceKey, property) in targets)
        {
            var current = registry.GetValue(RegistryHive2.LocalMachine, instanceKey, property);
            if (!IsEnabled(current, EnabledText)) return OptimizationState.NotApplied;
        }
        return OptimizationState.AppliedByCao;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        foreach (var (instanceKey, property) in TargetsOf(registry))
        {
            var raw = registry.GetValueRaw(RegistryHive2.LocalMachine, instanceKey, property, out var kind);
            snapshot.Registry.Add(new RegistrySnapshotEntry(
                RegistryHive2.LocalMachine.ToString(), instanceKey, property, raw,
                Existed: raw is not null)
            { Kind = raw is null ? RegistryValueKind2.None : kind });
        }
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var targets = TargetsOf(context.Registry);
        if (targets.Count == 0)
        {
            return Task.FromResult(OperationResult.Fail(
                "Ningún adaptador físico expone esta propiedad; no hay nada que cambiar.",
                "no-supported-adapter"));
        }

        var changed = 0;
        var skipped = 0;
        foreach (var (instanceKey, property) in targets)
        {
            var current = context.Registry.GetValue(RegistryHive2.LocalMachine, instanceKey, property);
            if (IsEnabled(current, EnabledText)) continue;
            context.Registry.GetValueRaw(RegistryHive2.LocalMachine, instanceKey, property, out var kind);
            var next = ToKindValue(current!, EnabledText, out var supported);
            if (!supported) { skipped++; continue; }
            context.Registry.SetValueRaw(RegistryHive2.LocalMachine, instanceKey, property, next, kind);
            changed++;
        }

        return Task.FromResult(changed > 0
            ? OperationResult.Ok($"Propiedad activada en {changed} adaptador(es).")
            : OperationResult.Fail("No se pudo activar la propiedad en ningún adaptador.", "apply-failed"));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        foreach (var entry in snapshot.Registry)
        {
            var hive = Enum.Parse<RegistryHive2>(entry.Hive);
            if (entry.Existed && entry.Value is not null)
            {
                context.Registry.SetValueRaw(hive, entry.KeyPath, entry.ValueName, entry.Value, entry.Kind);
            }
            else
            {
                context.Registry.DeleteValue(hive, entry.KeyPath, entry.ValueName);
            }
        }
        return Task.FromResult(OperationResult.Ok("Estado anterior de los adaptadores restaurado desde el snapshot."));
    }

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var observed = Detect(context.Registry);
        return Task.FromResult(observed switch
        {
            OptimizationState.AppliedByCao =>
                VerificationResult.Passed(observed, "Propiedad verificada como activa en los adaptadores."),
            OptimizationState.Unknown =>
                VerificationResult.Unknown(observed, "Sin adaptadores con esta propiedad; no verificable."),
            _ => VerificationResult.Failed(observed, "La propiedad no quedó activa tras aplicar."),
        });
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var lines = TargetsOf(registry).Select(t =>
        {
            var before = registry.GetValue(RegistryHive2.LocalMachine, t.InstanceKey, t.Property)?.ToString() ?? "(ausente)";
            return new PreviewLine
            {
                Kind = "Registry",
                Target = $@"HKLM\{t.InstanceKey}\{t.Property}",
                Before = before,
                After = EnabledText,
            };
        }).ToList();

        if (lines.Count == 0)
        {
            lines.Add(new PreviewLine
            {
                Kind = "Registry",
                Target = @"HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e972...}",
                Before = "sin adaptadores con esta propiedad",
                After = "sin cambios",
            });
        }

        var definition = Definition;
        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = definition.Id,
            Lines = lines,
            Risk = definition.Risk,
            SecurityImpact = definition.SecurityImpact,
            Flags = definition.Flags,
        });
    }
}
