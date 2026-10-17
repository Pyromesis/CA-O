using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Enables Auto HDR (SDR games converted to HDR on HDR-capable displays). No
/// performance impact; visual only. Hasta 23H2 el interruptor vive en
/// GameBar\AutoHdrToggleState; desde 24H2 (build 26100) la página Gráficos
/// lo guarda como AutoHDREnable=1 dentro de DirectXUserGlobalSettings, el
/// mismo valor combinado que VRR, preferencia de GPU y juegos en ventana.
/// Antes, en 24H2+ el Detect se rendía a Unknown y la optimización no se
/// podía verificar nunca. Se escriben ambas ubicaciones (la clásica no
/// estorba) y se verifica la que usa la build actual. Reversible exacto.
/// </summary>
public sealed class EnableAutoHdr : IOptimization
{
    internal const string GameBarKey = @"Software\Microsoft\GameBar";
    internal const string GameBarValue = "AutoHdrToggleState";
    internal const string DxKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    internal const string DxValue = "DirectXUserGlobalSettings";
    private const string Token = "AutoHDREnable";

    /// <summary>Build desde la que Auto HDR se guarda en DirectXUserGlobalSettings (24H2).</summary>
    internal const int RelocatedBuild = 26100;

    /// <summary>Build observada; sustituible en pruebas.</summary>
    internal static Func<int> CurrentBuild { get; set; } = () =>
    {
        try { return Environment.OSVersion.Version.Build; }
        catch { return 0; }
    };

    public OptimizationDefinition Definition => new()
    {
        Id = "enable-auto-hdr",
        NameEs = "Activar Auto HDR automático",
        NameEn = "Enable automatic Auto HDR",
        DescriptionEs = "Activa Auto HDR en Windows para convertir automáticamente juegos SDR a HDR. Solo funciona en pantallas compatibles con HDR. Sin impacto en rendimiento; solo mejora visual.",
        DescriptionEn = "Enables Auto HDR in Windows to automatically convert SDR games to HDR. Only works on HDR-capable displays. No performance impact; visual enhancement only.",
        TooltipEs = "Hasta 23H2 usa GameBar\\AutoHdrToggleState; desde 24H2, AutoHDREnable=1 dentro de DirectXUserGlobalSettings (respeta VRR y GPU preferida). Reinicia el juego. Reversible exacto.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        if (CurrentBuild() >= RelocatedBuild)
        {
            var combined = registry.GetValue(RegistryHive2.CurrentUser, DxKey, DxValue) as string;
            return ReadFlag(combined) == true ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;
        }

        return Equals(Normalize(registry.GetValue(RegistryHive2.CurrentUser, GameBarKey, GameBarValue)), 1L)
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        foreach (var (key, value) in new[] { (GameBarKey, GameBarValue), (DxKey, DxValue) })
        {
            var raw = registry.GetValueRaw(RegistryHive2.CurrentUser, key, value, out var kind);
            snapshot.Registry.Add(new RegistrySnapshotEntry(
                RegistryHive2.CurrentUser.ToString(), key, value, raw, Existed: raw is not null)
            { Kind = raw is null ? RegistryValueKind2.None : kind });
        }
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        context.Registry.SetValue(RegistryHive2.CurrentUser, GameBarKey, GameBarValue, 1, RegistryValueKind2.DWord);
        var combined = context.Registry.GetValue(RegistryHive2.CurrentUser, DxKey, DxValue) as string;
        context.Registry.SetValue(RegistryHive2.CurrentUser, DxKey, DxValue, WithFlag(combined, true), RegistryValueKind2.String);
        return Task.FromResult(OperationResult.Ok("Auto HDR automático habilitado. Reinicia el juego para verlo."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        foreach (var entry in snapshot.Registry)
        {
            if (entry.Existed && entry.Value is not null)
                context.Registry.SetValueRaw(RegistryHive2.CurrentUser, entry.KeyPath, entry.ValueName, entry.Value, entry.Kind);
            else
                context.Registry.DeleteValue(RegistryHive2.CurrentUser, entry.KeyPath, entry.ValueName);
        }
        return Task.FromResult(OperationResult.Ok("Estado anterior de Auto HDR restaurado."));
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Registry",
                    Target = $@"HKCU\{DxKey}\{DxValue}",
                    Before = registry.GetValue(RegistryHive2.CurrentUser, DxKey, DxValue) as string ?? "(ausente)",
                    After = WithFlag(registry.GetValue(RegistryHive2.CurrentUser, DxKey, DxValue) as string, true),
                },
                new PreviewLine
                {
                    Kind = "Registry",
                    Target = $@"HKCU\{GameBarKey}\{GameBarValue}",
                    Before = registry.GetValue(RegistryHive2.CurrentUser, GameBarKey, GameBarValue)?.ToString() ?? "(ausente)",
                    After = "1",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });

    internal static bool? ReadFlag(string? combined)
    {
        if (string.IsNullOrWhiteSpace(combined)) return null;
        var match = Regex.Match(combined, Token + @"\s*=\s*([01])", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value == "1" : null;
    }

    /// <summary>Pone AutoHDREnable sin tocar el resto de ajustes del valor combinado.</summary>
    internal static string WithFlag(string? combined, bool enabled)
    {
        var token = $"{Token}={(enabled ? 1 : 0)};";
        if (string.IsNullOrWhiteSpace(combined)) return token;
        if (Regex.IsMatch(combined, Token + @"\s*=\s*[01]", RegexOptions.IgnoreCase))
            return Regex.Replace(combined, Token + @"\s*=\s*[01]", token.TrimEnd(';'), RegexOptions.IgnoreCase);
        var trimmed = combined.TrimEnd(';', ' ');
        return trimmed.Length == 0 ? token : trimmed + ";" + token;
    }

    private static object? Normalize(object? value) => value switch
    {
        int i => (long)i,
        uint u => (long)u,
        long l => l,
        string s when long.TryParse(s, out var n) => n,
        _ => value,
    };
}
