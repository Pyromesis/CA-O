using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Restores Windows default GPU preference settings by removing CA-O
/// customizations token-by-token (GpuPreference, VRROptimizeEnable,
/// SwapEffectUpgradeEnable), preserving any other tokens in the combined
/// DirectXUserGlobalSettings value. The old pure DeleteValue wiped the
/// whole value and silently undid the 3 sister optimizations
/// (set-games-high-performance-gpu, enable-vrr,
/// enable-windowed-game-optimizations). Auditoría 2026-09-29 (C2).
/// </summary>
public sealed class RestoreDefaultGpuPreference : IOptimization
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ValueName = "DirectXUserGlobalSettings";

    internal static readonly IReadOnlyList<string> ManagedTokens =
        ["GpuPreference", "VRROptimizeEnable", "SwapEffectUpgradeEnable"];

    public OptimizationDefinition Definition => new()
    {
        Id = "restore-default-gpu-preference",
        NameEs = "Restaurar preferencia GPU por defecto",
        NameEn = "Restore default GPU preference",
        DescriptionEs = "Quita las personalizaciones de GPU de CA-O (preferencia, VRR, ventana) conservando el resto.",
        DescriptionEn = "Removes CA-O GPU customizations (preference, VRR, windowed) preserving the rest.",
        TooltipEs = "Limpia solo los tokens GpuPreference/VRROptimizeEnable/SwapEffectUpgradeEnable de HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences. Nunca borra el valor entero: respeta a sus 3 hermanas. Reversible via snapshot.",
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

    internal static bool HasManagedToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return ManagedTokens.Any(token =>
            Regex.IsMatch(value, $@"{token}\s*=", RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Quita los 3 tokens gestionados conservando el resto. Devuelve null
    /// cuando no queda ningún token (el llamador borra el valor).
    /// </summary>
    internal static string? StripManagedTokens(string? currentValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue))
            return null;
        var updated = currentValue;
        foreach (var token in ManagedTokens)
        {
            updated = Regex.Replace(updated, $@"{token}\s*=\s*[^;]*;?", string.Empty, RegexOptions.IgnoreCase);
        }
        updated = Regex.Replace(updated, @";{2,}", ";");
        updated = updated.Trim(' ', ';');
        return string.IsNullOrEmpty(updated) ? null : updated;
    }

    internal static string? GetToken(string? value, string token)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var match = Regex.Match(value, $@"{token}\s*=\s*([^;]*)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Restaura cada token gestionado a su estado del snapshot: si el
    /// snapshot lo tenía, se fija ese valor; si no, se quita. Los demás
    /// tokens actuales se conservan intactos.
    /// </summary>
    internal static string? MergeTokensToSnapshot(string? currentValue, string? snapshotValue)
    {
        var updated = currentValue ?? string.Empty;
        foreach (var token in ManagedTokens)
        {
            var wanted = GetToken(snapshotValue, token);
            updated = Regex.Replace(updated, $@"{token}\s*=\s*[^;]*;?", string.Empty, RegexOptions.IgnoreCase);
            if (wanted is not null)
            {
                updated = updated.TrimEnd(' ', ';');
                updated = string.IsNullOrEmpty(updated)
                    ? $"{token}={wanted};"
                    : $"{updated};{token}={wanted};";
            }
        }
        updated = Regex.Replace(updated, @";{2,}", ";").Trim(' ', ';');
        return string.IsNullOrEmpty(updated) ? null : updated;
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var value = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName) as string;
        // Aplicado = sin personalizaciones de CA-O (ausente o sin tokens).
        return HasManagedToken(value) ? OptimizationState.NotApplied : OptimizationState.AppliedByCao;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        var existing = registry.GetValueRaw(RegistryHive2.CurrentUser, KeyPath, ValueName, out var kind);
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            RegistryHive2.CurrentUser.ToString(),
            KeyPath,
            ValueName,
            existing,
            Existed: existing is not null)
        { Kind = existing is null ? RegistryValueKind2.None : kind });
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var current = context.Registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName) as string;
        var stripped = StripManagedTokens(current);
        if (stripped is null)
            context.Registry.DeleteValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        else
            context.Registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, stripped, RegistryValueKind2.String);
        return Task.FromResult(OperationResult.Ok("Preferencia GPU restaurada a valores predeterminados (tokens de CA-O retirados, resto conservado)."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        var entry = snapshot.Registry.FirstOrDefault(e =>
            e.ValueName == ValueName && e.KeyPath == KeyPath);
        var snapshotValue = entry is { Existed: true } ? entry.Value?.ToString() : null;
        var current = context.Registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName) as string;
        var merged = MergeTokensToSnapshot(current, snapshotValue);
        if (merged is null)
            context.Registry.DeleteValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        else
            context.Registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, merged, RegistryValueKind2.String);
        return Task.FromResult(OperationResult.Ok("Tokens de GPU restaurados a su snapshot (resto conservado)."));
    }
}
