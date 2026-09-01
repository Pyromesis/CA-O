using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Restores Windows default GPU preference settings by removing customizations.
/// Clears or resets HKCU\Software\Microsoft\DirectX\UserGpuPreferences entries.
/// </summary>
public sealed class RestoreDefaultGpuPreference : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "restore-default-gpu-preference",
        NameEs = "Restaurar preferencia GPU por defecto",
        NameEn = "Restore default GPU preference",
        DescriptionEs = "Restaura la configuración predeterminada de preferencia de GPU eliminando personalizaciones.",
        DescriptionEn = "Restores default GPU preference settings by removing customizations.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var value = registry.GetValue(RegistryHive2.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings");
        // Detect is "applied" when the key is default or missing
        return value is null ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        var existing = registry.GetValueRaw(RegistryHive2.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", out var kind);
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            RegistryHive2.CurrentUser.ToString(),
            @"Software\Microsoft\DirectX\UserGpuPreferences",
            "DirectXUserGlobalSettings",
            existing,
            Existed: existing is not null)
        { Kind = existing is null ? RegistryValueKind2.None : kind });
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        context.Registry.DeleteValue(RegistryHive2.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings");
        return Task.FromResult(OperationResult.Ok("Preferencia GPU restaurada a valores predeterminados."));
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
        return Task.FromResult(OperationResult.Ok("Preferencia GPU restaurada desde snapshot."));
    }
}
