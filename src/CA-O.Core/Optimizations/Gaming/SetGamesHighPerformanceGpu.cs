using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Configures Windows to prefer high-performance (dedicated) GPU for games.
/// Modifies HKCU\Software\Microsoft\DirectX\UserGpuPreferences registry settings.
/// Merge-preserving: solo toca el token GpuPreference y conserva el resto
/// (VRR, windowed optimizations) — nunca clobber como antes.
/// </summary>
public sealed class SetGamesHighPerformanceGpu : IOptimization
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ValueName = "DirectXUserGlobalSettings";
    private const string GpuHighPerf = "GpuPreference=2;";

    public OptimizationDefinition Definition => new()
    {
        Id = "set-games-high-performance-gpu",
        NameEs = "Preferir GPU dedicada para juegos",
        NameEn = "Preferir GPU dedicada para juegos",
        DescriptionEs = "Configura Windows para preferir GPU dedicada en juegos. Beneficio potencial: mejor rendimiento en juegos con GPU dedicada disponible.",
        DescriptionEn = "Configures Windows to prefer dedicated GPU for games. Potential benefit: improved performance in games with dedicated GPU.",
        TooltipEs = "Modifica HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences. Reversible via snapshot.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var current = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName) as string;
        if (current is null)
        {
            return OptimizationState.NotApplied;
        }
        var match = Regex.Match(current, @"GpuPreference\s*=\s*([0-9]+)", RegexOptions.IgnoreCase);
        return match.Success && match.Groups[1].Value == "2"
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        var value = registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            RegistryHive2.CurrentUser.ToString(), KeyPath, ValueName, value,
            Existed: value is not null)
        { Kind = RegistryValueKind2.String });
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var current = context.Registry.GetValue(RegistryHive2.CurrentUser, KeyPath, ValueName) as string;
        var updated = UpdateGpuPreference(current);
        context.Registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, updated, RegistryValueKind2.String);
        return Task.FromResult(OperationResult.Ok("GPU dedicada configurada como preferencia para juegos."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        var entry = snapshot.Registry.FirstOrDefault(e =>
            e.ValueName == ValueName && e.KeyPath == KeyPath);
        if (entry is not null && entry.Existed && entry.Value is not null)
        {
            context.Registry.SetValue(RegistryHive2.CurrentUser, KeyPath, ValueName, entry.Value.ToString()!, RegistryValueKind2.String);
            return Task.FromResult(OperationResult.Ok("Preferencia de GPU original restaurada."));
        }
        context.Registry.DeleteValue(RegistryHive2.CurrentUser, KeyPath, ValueName);
        return Task.FromResult(OperationResult.Ok("Preferencia de GPU original restaurada (valor no existía previamente)."));
    }

    /// <summary>
    /// Fija GpuPreference=2 conservando el resto de tokens (VRR, SwapEffect...).
    /// </summary>
    public static string UpdateGpuPreference(string? currentValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return GpuHighPerf;
        }
        if (Regex.IsMatch(currentValue, @"GpuPreference\s*=\s*[0-9]+", RegexOptions.IgnoreCase))
        {
            return Regex.Replace(currentValue, @"GpuPreference\s*=\s*[0-9]+", "GpuPreference=2", RegexOptions.IgnoreCase);
        }
        var trimmed = currentValue.TrimEnd(';', ' ');
        if (string.IsNullOrEmpty(trimmed))
        {
            return GpuHighPerf;
        }
        return trimmed + ";" + GpuHighPerf;
    }
}
