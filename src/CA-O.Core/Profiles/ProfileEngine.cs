using CAO.Shared;

namespace CAO.Core.Profiles;

/// <summary>Outcome of profile gating for one optimization.</summary>
public sealed record ProfileDecision(bool Allowed, string ReasonEs);

/// <summary>
/// Profile engine (spec 104-106): profiles are not fixed lists. Each profile
/// consults hardware, Windows build, security posture, anti-cheat presence
/// and current state through the SystemContext before allowing anything.
/// </summary>
public static class ProfileEngine
{
    public static IReadOnlyList<ProfileId> All { get; } = new[]
    {
        ProfileId.Safe, ProfileId.Balanced, ProfileId.Gaming, ProfileId.Competitive,
        ProfileId.Productivity, ProfileId.PowerSaver,
        ProfileId.Privacy, ProfileId.Security, ProfileId.Maintenance,
        ProfileId.Expert, ProfileId.Custom,
    };

    public static ProfileDecision Evaluate(ProfileId profile, OptimizationDefinition definition, SystemContext context)
    {
        // Anti-cheat and default-block guards apply to every profile except Expert.
        if (profile != ProfileId.Expert)
        {
            var guard = Gaming.AntiCheatGuard.Evaluate(definition, context);
            if (guard.Code is "blocked-by-default" or "blocked-anticheat" or "blocked-anticheat-category")
            {
                return new(false, guard.MessageEs);
            }
        }

        return profile switch
        {
            ProfileId.Safe => EvaluateSafe(definition, context),
            ProfileId.Balanced => EvaluateBalanced(definition),
            ProfileId.Gaming => EvaluateGaming(definition, context),
            ProfileId.Competitive => EvaluateCompetitive(definition, context),
            ProfileId.Productivity => EvaluateProductivity(definition, context),
            ProfileId.PowerSaver => EvaluatePowerSaver(definition, context),
            ProfileId.Privacy => EvaluatePrivacy(definition),
            ProfileId.Security => EvaluateSecurity(definition),
            ProfileId.Maintenance => EvaluateMaintenance(definition),
            ProfileId.Expert => new(true, "Modo Expert: requiere confirmación explícita y snapshot."),
            ProfileId.Custom => new(true, "Selección manual del usuario; se muestran todos los detalles."),
            _ => new(false, "Perfil desconocido."),
        };
    }

    /// <summary>Safe: high confidence, low risk, zero security degradation (spec 14).</summary>
    private static ProfileDecision EvaluateSafe(OptimizationDefinition definition, SystemContext context)
    {
        if (definition.SecurityImpact != SecurityImpact.None)
        {
            return new(false, "Safe no permite cambios que toquen seguridad.");
        }

        if (definition.Risk is not (RiskLevel.Safe or RiskLevel.Low))
        {
            return new(false, "Safe sólo permite riesgo bajo o nulo.");
        }

        if (definition.Evidence is EvidenceLevel.Unknown or EvidenceLevel.Heuristic)
        {
            return new(false, "Safe exige evidencia establecida.");
        }

        if (definition.Compatibility != CompatibilityStatus.Compatible)
        {
            return new(false, "Safe exige compatibilidad plenamente conocida.");
        }

        var precondition = Compatibility.Rules.EvaluatePreconditions(definition, context);
        return precondition.Passed
            ? new(true, "Apto para el perfil Safe.")
            : new(false, precondition.ReasonEs);
    }

    /// <summary>Balanced: the classic Recommended policy.</summary>
    private static ProfileDecision EvaluateBalanced(OptimizationDefinition definition)
    {
        if (definition.Flags.HasFlag(OptimizationFlags.ExpertOnly))
        {
            return new(false, "Requiere modo Expert.");
        }

        if (definition.SecurityImpact == SecurityImpact.ReducedProtection ||
            definition.Flags.HasFlag(OptimizationFlags.SecurityTradeoff))
        {
            return new(false, "Reduce seguridad; bloqueado en Balanced.");
        }

        if (definition.Evidence is EvidenceLevel.Unknown or EvidenceLevel.Heuristic)
        {
            return new(false, "Sin evidencia suficiente para Balanced.");
        }

        return new(true, "Apto para el perfil Balanced.");
    }

    /// <summary>Gaming: Balanced + gaming categories, battery-aware (spec 106).</summary>
    private static ProfileDecision EvaluateGaming(OptimizationDefinition definition, SystemContext context)
    {
        var balanced = EvaluateBalanced(definition);
        if (!balanced.Allowed)
        {
            return balanced;
        }

        if (context.IsLaptop && context.OnBattery &&
            definition.ExpectedImpact is PerformanceImpact.Moderate or PerformanceImpact.Large)
        {
            return new(false, "Con batería se conservan los valores de eficiencia.");
        }

        if (context.ThermalState == ThermalState.Throttling &&
            definition.Category == OptimizationCategory.Performance)
        {
            return new(false, "Throttling térmico activo: resolver refrigeración primero.");
        }

        return new(true, "Apto para el perfil Gaming.");
    }

    /// <summary>Competitive: latency-focused but never security-reducing.</summary>
    private static ProfileDecision EvaluateCompetitive(OptimizationDefinition definition, SystemContext context)
    {
        var gaming = EvaluateGaming(definition, context);
        if (!gaming.Allowed)
        {
            return gaming;
        }

        if (definition.SecurityImpact != SecurityImpact.None &&
            definition.Category is OptimizationCategory.PrivacySecurity)
        {
            return new(false, "Competitive no modifica seguridad; sólo latencia/rendimiento.");
        }

        return new(true, "Apto para el perfil Competitive.");
    }

    /// <summary>Productivity: Balanced scope without gaming-only changes; battery aware.</summary>
    private static ProfileDecision EvaluateProductivity(OptimizationDefinition definition, SystemContext context)
    {
        var balanced = EvaluateBalanced(definition);
        if (!balanced.Allowed)
        {
            return balanced;
        }

        if (context.OnBattery && definition.ExpectedImpact is PerformanceImpact.Moderate or PerformanceImpact.Large)
        {
            return new(false, "Con batería se conservan los valores de eficiencia.");
        }

        return new(true, "Apto para el perfil Productivity.");
    }

    /// <summary>Power Saver: only zero/low-impact efficiency-friendly work.</summary>
    private static ProfileDecision EvaluatePowerSaver(OptimizationDefinition definition, SystemContext context)
    {
        if (definition.ExpectedImpact is PerformanceImpact.Moderate or PerformanceImpact.Large)
        {
            return new(false, "Power Saver no aplica cambios de impacto medio/alto.");
        }

        if (definition.Category is OptimizationCategory.Gaming)
        {
            return new(false, "Power Saver no ejecuta cambios orientados a gaming.");
        }

        if (context.ThermalState == ThermalState.Throttling)
        {
            return new(false, "Throttling térmico activo: resolver refrigeración primero.");
        }

        var balanced = EvaluateBalanced(definition);
        return balanced.Allowed
            ? new(true, "Apto para el perfil Power Saver.")
            : balanced;
    }

    private static ProfileDecision EvaluatePrivacy(OptimizationDefinition definition) =>
        definition.Category is OptimizationCategory.PrivacySecurity
            ? new(true, "Cambio de privacidad gestionado por este perfil.")
            : new(false, "El perfil Privacy sólo gestiona privacidad/telemetría.");

    private static ProfileDecision EvaluateSecurity(OptimizationDefinition definition)
    {
        if (definition.SecurityImpact == SecurityImpact.ReducedProtection ||
            definition.Flags.HasFlag(OptimizationFlags.SecurityTradeoff))
        {
            return new(false, "El perfil Security nunca reduce protecciones.");
        }

        return definition.SecurityImpact == SecurityImpact.IncreasedProtection ||
               definition.Category == OptimizationCategory.PrivacySecurity
            ? new(true, "Refuerzo de seguridad permitido.")
            : new(false, "El perfil Security sólo gestiona cambios de seguridad.");
    }

    private static ProfileDecision EvaluateMaintenance(OptimizationDefinition definition) =>
        definition.Category == OptimizationCategory.Storage ||
        definition.Flags.HasFlag(OptimizationFlags.NotReversible)
            ? new(true, "Tarea de mantenimiento permitida.")
            : new(false, "El perfil Maintenance sólo ejecuta limpieza y almacenamiento.");
}
