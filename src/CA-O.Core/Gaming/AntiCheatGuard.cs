using CAO.Shared;

namespace CAO.Core.Gaming;

/// <summary>
/// Hard blocks that apply BEFORE profile logic (spec 59, 95, 144). Anything
/// on this list can only run in Expert mode with explicit confirmation and a
/// snapshot; when Vanguard is present the block is absolute for
/// security-reducing changes.
/// </summary>
public static class AntiCheatGuard
{
    /// <summary>Optimization ids that must never be auto-recommended (spec 95).</summary>
    public static readonly IReadOnlySet<string> NeverAutoRecommend = new HashSet<string>(StringComparer.Ordinal)
    {
        "disable-vbs",
        "disable-hvci",
        "disable-memory-integrity",
        "disable-memory-compression",
        "disable-core-parking",
        "cpu-min-state-100",
        "disable-cpu-idle",
        "disable-power-throttling",
        "disable-search-service",
        "disable-bits",
        "disable-delivery-optimization",
        "disable-automatic-maintenance",
        "disable-mpo",
        "disable-fullscreen-optimizations-global",
        "static-pagefile",
        "network-throttling-index-hack",
        "svchost-split-threshold-hack",
        "delete-prefetch",
        "hypervisor-launchtype-off",
    };

    /// <summary>
    /// Categories of change that can interfere with kernel anti-cheats;
    /// blocked by default whenever any anti-cheat is detected (spec 60).
    /// </summary>
    private static readonly IReadOnlySet<OptimizationCategory> AntiCheatSensitiveCategories =
        new HashSet<OptimizationCategory> { OptimizationCategory.PrivacySecurity };

    public static RecommendationReason Evaluate(OptimizationDefinition definition, SystemContext context)
    {
        var antiCheatPresent = context.AntiCheats.Count > 0;
        var reducesSecurity = definition.SecurityImpact == SecurityImpact.ReducedProtection ||
                              definition.Flags.HasFlag(OptimizationFlags.SecurityTradeoff);

        // Most specific reason wins so the user sees WHY it was blocked.
        if (antiCheatPresent && reducesSecurity)
        {
            return new RecommendationReason(
                "blocked-anticheat",
                $"Anti-cheat detectado ({string.Join(", ", context.AntiCheats.Select(a => a.Kind))}): cambio bloqueado por defecto.");
        }

        if (antiCheatPresent && AntiCheatSensitiveCategories.Contains(definition.Category) &&
            definition.Risk is RiskLevel.High or RiskLevel.Critical)
        {
            return new RecommendationReason(
                "blocked-anticheat-category",
                "Riesgo alto en una categoría sensible a anti-cheats; bloqueado por defecto.");
        }

        if (NeverAutoRecommend.Contains(definition.Id))
        {
            return new RecommendationReason(
                "blocked-by-default",
                "Lista de bloqueo por defecto: requiere diagnóstico, benchmark y modo Expert.");
        }

        if (antiCheatPresent)
        {
            return new RecommendationReason(
                "anticheat-caution",
                "Sin conflicto conocido con el anti-cheat detectado; clasificación conservadora aplicada.");
        }

        return new RecommendationReason("pass", "Sin conflictos conocidos con anti-cheats.");
    }

    /// <summary>True when the guard fully prevents automatic recommendation.</summary>
    public static bool IsHardBlocked(OptimizationDefinition definition, SystemContext context) =>
        Evaluate(definition, context) is { Code: "blocked-by-default" or "blocked-anticheat" or "blocked-anticheat-category" };
}
