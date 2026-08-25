using CAO.Shared;

namespace CAO.Core.Compatibility;

/// <summary>
/// Build/hardware gating rules applied before any change is offered
/// (spec 19: never apply a tweak on unsupported/unknown/broken builds;
/// spec 35: never add performance tweaks while thermal throttling).
/// </summary>
public static class Rules
{
    /// <summary>Builds known to break specific tweaks would live here; empty today.</summary>
    private static readonly IReadOnlySet<int> KnownBrokenBuilds = new HashSet<int>();

    public static PreconditionResult EvaluatePreconditions(OptimizationDefinition definition, SystemContext context)
    {
        if (context.WindowsBuild <= 0)
        {
            return PreconditionResult.Fail("La versión de Windows no pudo determinarse; no se aplican cambios a ciegas.");
        }

        if (KnownBrokenBuilds.Contains(context.WindowsBuild))
        {
            return PreconditionResult.Fail($"Windows build {context.WindowsBuild} tiene un problema conocido con este cambio.");
        }

        if (context.WindowsBuild < 22000 &&
            definition.Category is OptimizationCategory.Gaming)
        {
            return PreconditionResult.Fail("Esta optimización de gaming está dirigida a Windows 11.");
        }

        if (definition.Flags.HasFlag(OptimizationFlags.RecommendedOnSsd) && !context.HasSsd)
        {
            return PreconditionResult.Fail("Requiere almacenamiento SSD/NVMe.");
        }

        if (context.ThermalState == ThermalState.Throttling &&
            definition.ExpectedImpact is not (PerformanceImpact.DiagnosticOnly or PerformanceImpact.None))
        {
            return PreconditionResult.Fail("Throttling térmico detectado: resuelva la refrigeración antes de aplicar cambios de rendimiento.");
        }

        if (context.IsLaptop && context.OnBattery &&
            definition.Risk is RiskLevel.High or RiskLevel.Critical)
        {
            return PreconditionResult.Fail("No se aplican cambios de riesgo alto con batería en un equipo portátil.");
        }

        if (definition.SecurityImpact == SecurityImpact.ReducedProtection && context.VanguardDetected)
        {
            return PreconditionResult.Fail("Anti-cheat sensible detectado (Vanguard): los cambios que reducen seguridad están bloqueados por defecto.");
        }

        return PreconditionResult.Ok("Precondiciones cumplidas para este contexto de sistema.");
    }
}
