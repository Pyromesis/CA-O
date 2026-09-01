using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// #20: Enable "Optimizations for windowed games" (Windows 11 22H2+).
/// Sets HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings = "SwapEffectUpgradeEnable=1;"
/// This enables DWM composition optimizations for DX10/DX11 windowed/borderless games, reducing latency and enabling Auto HDR/VRR.
/// </summary>
public sealed class EnableWindowedGameOptimizations : IOptimization
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ValueName = "DirectXUserGlobalSettings";
    private const string EnabledValue = "SwapEffectUpgradeEnable=1;";
    private const string DisabledValue = "SwapEffectUpgradeEnable=0;";

    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Activar optimizaciones para juegos en ventana",
        NameEn = "Enable optimizations for windowed games",
        DescriptionEs = "Habilita optimizaciones de DWM para juegos DX10/11 en modo ventana/borderless (reduce latencia, habilita Auto HDR/VRR). Requiere Windows 11 22H2+.",
        DescriptionEn = "Enables DWM composition optimizations for DX10/DX11 windowed/borderless games (reduces latency, enables Auto HDR/VRR). Requires Windows 11 22H2+.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences\\DirectXUserGlobalSettings. Requiere reiniciar el juego para aplicar. Reversible exacto.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.NoKnownConflict,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
        Reversible = true,
        Flags = OptimizationFlags.None,
    };

    private static string Id => "enable-windowed-game-optimizations";

    /// <summary>Current value injected by engine from live registry before Detect/Capture.</summary>
    public string? ObservedValue { get; set; }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var current = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName) as string;
        if (current is null)
        {
            return OptimizationState.NotApplied;
        }
        var normalized = NormalizeDxValue(current);
        return normalized == EnabledValue
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        var value = ObservedValue ?? registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            RegistryHive2.CurrentUser.ToString(), KeyPath, ValueName, value,
            Existed: value is not null)
        { Kind = RegistryValueKind2.String });
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        context.Registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, EnabledValue, RegistryValueKind2.String);
        return Task.FromResult(OperationResult.Ok("Optimizaciones para juegos en ventana activadas. Reinicie el juego para que surta efecto."));
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        var entry = snapshot.Registry.FirstOrDefault(e =>
            e.ValueName == ValueName && e.KeyPath == KeyPath);

        if (entry is not null && entry.Existed && entry.Value is not null)
        {
            context.Registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, entry.Value.ToString()!, RegistryValueKind2.String);
            return OperationResult.Ok("Configuración original de optimizaciones para juegos en ventana restaurada.");
        }

        // Value didn't exist before - delete it to restore exact original state
        context.Registry.DeleteValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        return OperationResult.Ok("Configuración original restaurada (valor no existía previamente).");
    }

    /// <summary>
    /// Normalizes the DX UserGpuPreferences value to canonical form for comparison.
    /// Handles variations like "SwapEffectUpgradeEnable=1" vs "SwapEffectUpgradeEnable=1;".
    /// </summary>
    private static string NormalizeDxValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DisabledValue;
        }

        var match = Regex.Match(value, @"SwapEffectUpgradeEnable\s*=\s*([01])", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return DisabledValue;
        }

        return match.Groups[1].Value == "1" ? EnabledValue : DisabledValue;
    }
}
