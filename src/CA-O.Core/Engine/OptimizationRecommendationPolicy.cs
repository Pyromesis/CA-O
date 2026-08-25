using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Engine;

public enum RecommendationProfile
{
    Recommended,
    SafeGaming,
    Expert,
}

public sealed record RecommendationDecision(bool Allowed, string ReasonEs);

public static class OptimizationRecommendationPolicy
{
    public static RecommendationDecision Evaluate(
        OptimizationDefinition definition,
        RecommendationProfile profile,
        SystemInfoReport? system = null)
    {
        if (profile == RecommendationProfile.Expert)
        {
            return new(true, "Disponible en modo Expert con confirmación explícita.");
        }

        if (definition.Flags.HasFlag(OptimizationFlags.ExpertOnly))
        {
            return new(false, "Requiere modo Expert.");
        }

        if (definition.Flags.HasFlag(OptimizationFlags.SecurityTradeoff) ||
            definition.SecurityImpact == SecurityImpact.ReducedProtection)
        {
            return new(false, "Reduce protección de seguridad y está bloqueada por defecto.");
        }

        if (definition.Evidence is EvidenceLevel.Unknown or EvidenceLevel.Heuristic)
        {
            return new(false, "No tiene evidencia suficiente para una recomendación automática.");
        }

        if (definition.Compatibility is CompatibilityStatus.Unknown or CompatibilityStatus.PotentialConflict or CompatibilityStatus.Incompatible)
        {
            return new(false, "La compatibilidad no está suficientemente establecida.");
        }

        if (profile == RecommendationProfile.SafeGaming &&
            (definition.Risk is not (RiskLevel.Safe or RiskLevel.Low) ||
             definition.SecurityImpact != SecurityImpact.None ||
             definition.Compatibility != CompatibilityStatus.Compatible))
        {
            return new(false, "Safe Gaming sólo permite cambios de bajo riesgo y compatibilidad conocida.");
        }

        if (definition.Flags.HasFlag(OptimizationFlags.RecommendedOnSsd) && system is { HasSsd: false })
        {
            return new(false, "Esta recomendación requiere almacenamiento SSD.");
        }

        return new(true, "Cumple los controles de recomendación del perfil.");
    }
}