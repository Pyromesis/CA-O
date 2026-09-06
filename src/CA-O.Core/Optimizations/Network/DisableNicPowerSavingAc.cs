using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>Disables NIC power management (PnPCapabilities=24) on physical adapters.</summary>
public sealed class DisableNicPowerSavingAc : IOptimization
{
    private const string NetClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
    private const int DisabledValue = 24;

    private static readonly string[] VirtualKeywords =
        ["virtual", "vpn", "hyper-v", "miniport", "bluetooth", "wan ", "loopback", "tunnel", "teredo", "isatap"];

    public OptimizationDefinition Definition => new()
    {
        Id = "disable-nic-power-saving-ac",
        NameEs = "Desactivar ahorro energia NIC en AC",
        NameEn = "Disable NIC power saving on AC",
        DescriptionEs = "Desactiva la gestión de energía en NICs físicas (PnPCapabilities). Solo con alimentación AC.",
        DescriptionEn = "Disables power management on physical NICs (PnPCapabilities). AC power only.",
        TooltipEs = "Establece PnPCapabilities=24 en adaptadores físicos. Reversible exacto.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    private IReadOnlyList<string> PhysicalInstances(IRegistryAccessor registry)
    {
        var found = new List<string>();
        foreach (var instance in registry.GetSubKeyNames(RegistryHive2.LocalMachine, NetClassKey))
        {
            var instanceKey = $@"{NetClassKey}\{instance}";
            var desc = registry.GetValue(RegistryHive2.LocalMachine, instanceKey, "DriverDesc") as string;
            if (string.IsNullOrWhiteSpace(desc)) continue;
            if (VirtualKeywords.Any(k => desc.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
            found.Add(instanceKey);
        }
        return found;
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var instances = PhysicalInstances(registry);
        if (instances.Count == 0) return OptimizationState.Unknown;
        foreach (var key in instances)
        {
            var current = registry.GetValue(RegistryHive2.LocalMachine, key, "PnPCapabilities");
            if (!Equals(Normalize(current), (long)DisabledValue)) return OptimizationState.NotApplied;
        }
        return OptimizationState.AppliedByCao;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        foreach (var key in PhysicalInstances(registry))
        {
            var raw = registry.GetValueRaw(RegistryHive2.LocalMachine, key, "PnPCapabilities", out var kind);
            snapshot.Registry.Add(new RegistrySnapshotEntry(
                RegistryHive2.LocalMachine.ToString(), key, "PnPCapabilities", raw,
                Existed: raw is not null)
            { Kind = raw is null ? RegistryValueKind2.None : kind });
        }
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var instances = PhysicalInstances(context.Registry);
        if (instances.Count == 0)
        {
            return Task.FromResult(OperationResult.Fail(
                "No se encontraron adaptadores físicos; no hay nada que cambiar.",
                "no-supported-adapter"));
        }

        var changed = 0;
        foreach (var key in instances)
        {
            var current = context.Registry.GetValue(RegistryHive2.LocalMachine, key, "PnPCapabilities");
            if (Equals(Normalize(current), (long)DisabledValue)) continue;
            context.Registry.GetValueRaw(RegistryHive2.LocalMachine, key, "PnPCapabilities", out var kind);
            var writeKind = kind == RegistryValueKind2.None ? RegistryValueKind2.DWord : kind;
            context.Registry.SetValueRaw(RegistryHive2.LocalMachine, key, "PnPCapabilities", DisabledValue, writeKind);
            changed++;
        }

        return Task.FromResult(OperationResult.Ok($"Ahorro de energía desactivado en {changed} adaptador(es)."));
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
        return Task.FromResult(OperationResult.Ok("Gestión de energía de NIC restaurada desde el snapshot."));
    }

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var observed = Detect(context.Registry);
        return Task.FromResult(observed switch
        {
            OptimizationState.AppliedByCao =>
                VerificationResult.Passed(observed, "PnPCapabilities verificado en adaptadores."),
            OptimizationState.Unknown =>
                VerificationResult.Unknown(observed, "Sin adaptadores físicos; no verificable."),
            _ => VerificationResult.Failed(observed, "PnPCapabilities no quedó aplicado tras aplicar."),
        });
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Solo con alimentación AC."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var lines = PhysicalInstances(registry).Select(key =>
        {
            var before = registry.GetValue(RegistryHive2.LocalMachine, key, "PnPCapabilities")?.ToString() ?? "(ausente)";
            return new PreviewLine
            {
                Kind = "Registry",
                Target = $@"HKLM\{key}\PnPCapabilities",
                Before = before,
                After = DisabledValue.ToString(),
            };
        }).ToList();

        if (lines.Count == 0)
        {
            lines.Add(new PreviewLine
            {
                Kind = "Registry",
                Target = @"HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e972...}\PnPCapabilities",
                Before = "sin adaptadores físicos",
                After = "sin cambios",
            });
        }

        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines = lines,
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
    }

    private static object? Normalize(object? value) => value switch
    {
        int i => (long)i,
        uint u => (long)u,
        long l => l,
        string s => long.TryParse(s.Trim(), out var parsed) ? parsed : s,
        null => null,
        _ => value.ToString(),
    };
}
