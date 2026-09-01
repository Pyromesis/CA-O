using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// #21: Enable Variable Refresh Rate (VRR) for games (Windows 11 22H2+).
/// Sets HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings VRROptimizeEnable=1
/// This enables OS-level VRR support for DX11 fullscreen games that lack native VRR support.
/// Shares the same registry value as enable-windowed-game-optimizations; both are preserved.
/// </summary>
public sealed class EnableVrr : IOptimization
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ValueName = "DirectXUserGlobalSettings";
    private const string VrrEnabled = "VRROptimizeEnable=1;";
    private const string VrrDisabled = "VRROptimizeEnable=0;";

    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Activar Variable Refresh Rate (VRR)",
        NameEn = "Enable Variable Refresh Rate (VRR)",
        DescriptionEs = "Habilita VRR a nivel de OS para juegos DX11 fullscreen sin soporte nativo de VRR. Requiere monitor compatible con G-Sync/FreeSync/Adaptive-Sync y Windows 11 22H2+.",
        DescriptionEn = "Enables OS-level VRR support for DX11 fullscreen games without native VRR. Requires VRR-capable monitor (G-Sync/FreeSync/Adaptive-Sync) and Windows 11 22H2+.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences\\DirectXUserGlobalSettings (VRROptimizeEnable=1). Requiere reiniciar el juego. Compatible con optimizaciones para juegos en ventana (mismo valor, distinta clave). Reversible exacto.",
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

    private static string Id => "enable-vrr";

    /// <summary>Current value injected by engine from live registry before Detect/Capture.</summary>
    public string? ObservedValue { get; set; }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var current = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName) as string;
        if (current is null)
        {
            return OptimizationState.NotApplied;
        }
        var normalized = NormalizeVrrValue(current);
        return normalized == VrrEnabled
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
        var current = context.Registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName) as string;
        var updated = UpdateVrrSetting(current, true);
        context.Registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, updated, RegistryValueKind2.String);
        return Task.FromResult(OperationResult.Ok("Variable Refresh Rate activado. Reinicie el juego para que surta efecto."));
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        var entry = snapshot.Registry.FirstOrDefault(e =>
            e.ValueName == ValueName && e.KeyPath == KeyPath);

        if (entry is not null && entry.Existed && entry.Value is not null)
        {
            context.Registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, entry.Value.ToString()!, RegistryValueKind2.String);
            return OperationResult.Ok("Configuración original de VRR restaurada.");
        }

        // Value didn't exist before - delete it to restore exact original state
        context.Registry.DeleteValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        return OperationResult.Ok("Configuración original restaurada (valor no existía previamente).");
    }

    /// <summary>
    /// Normalizes the VRR setting from the combined DirectXUserGlobalSettings value.
    /// </summary>
    private static string NormalizeVrrValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return VrrDisabled;
        }

        var match = Regex.Match(value, @"VRROptimizeEnable\s*=\s*([01])", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return VrrDisabled;
        }

        return match.Groups[1].Value == "1" ? VrrEnabled : VrrDisabled;
    }

    /// <summary>
    /// Updates the VRR setting in the combined value while preserving other settings (like SwapEffectUpgradeEnable).
    /// </summary>
    private static string UpdateVrrSetting(string? currentValue, bool enable)
    {
        var target = enable ? VrrEnabled : VrrDisabled;
        
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return target;
        }

        // Check if VRROptimizeEnable already exists in the value
        var vrrMatch = Regex.Match(currentValue, @"VRROptimizeEnable\s*=\s*[01]", RegexOptions.IgnoreCase);
        if (vrrMatch.Success)
        {
            // Replace existing VRR setting
            return Regex.Replace(currentValue, @"VRROptimizeEnable\s*=\s*[01]", target.TrimEnd(';'), RegexOptions.IgnoreCase);
        }

        // No VRR setting exists - append it (ensure semicolon separation)
        var trimmed = currentValue.TrimEnd(';', ' ');
        if (string.IsNullOrEmpty(trimmed))
        {
            return target;
        }
        return trimmed + ";" + target;
    }
}
