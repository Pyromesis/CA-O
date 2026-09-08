using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimization;

/// <summary>
/// Lectura del plan de energía activo desde el registro (HKLM, legible sin
/// elevación): powercfg mantiene sincronizado
/// SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\ActivePowerScheme.
/// Permite un Detect honesto para planes (exclusión mutua natural: solo el
/// plan activo reporta AppliedByCao) sin ejecutor en Detect.
/// </summary>
public static class PowerSchemes
{
    public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string UltimatePerformanceGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    private const string SchemesKey = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
    private const string ActiveValue = "ActivePowerScheme";

    /// <summary>Resuelve alias powercfg (SCHEME_MAX/BALANCED) a GUID canónico.</summary>
    public static string ResolveSchemeGuid(string schemeOrAlias) => schemeOrAlias switch
    {
        "SCHEME_MAX" => HighPerformanceGuid,
        "SCHEME_BALANCED" => BalancedGuid,
        "SCHEME_MIN" => "a1841308-3541-4fab-bc81-f71556f20b4a",
        _ => schemeOrAlias,
    };

    /// <summary>GUID del plan activo o null si no se puede leer.</summary>
    public static string? ReadActiveScheme(IRegistryAccessor registry)
    {
        try
        {
            var value = registry.GetValue(RegistryHive2.LocalMachine, SchemesKey, ActiveValue)?.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// AppliedByCao ssi el plan activo es uno de los esperados; Unknown si no
    /// se puede leer (nunca éxito falso, nunca NotApplied falso).
    /// </summary>
    public static OptimizationState DetectScheme(IRegistryAccessor registry, params string[] expectedGuids)
    {
        var active = ReadActiveScheme(registry);
        if (active is null)
        {
            return OptimizationState.Unknown;
        }
        return expectedGuids.Any(g => string.Equals(active, g, StringComparison.OrdinalIgnoreCase))
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;
    }

    /// <summary>Nota scheme= para snapshot (revertir el plan previo exacto).</summary>
    public static string CaptureSchemeNote(IRegistryAccessor registry) =>
        $"scheme={ReadActiveScheme(registry) ?? BalancedGuid}";
}
