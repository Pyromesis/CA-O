using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimization;

/// <summary>
/// Grupos de optimizaciones mutuamente excluyentes: solo un miembro puede
/// estar activo a la vez (p. ej. planes de energía: activar Alto y después
/// Equilibrado no acumula, se pisan). El motor rechaza el segundo con
/// mensaje explícito y la UI lo muestra candado.
/// </summary>
public static class OptimizationConflicts
{
    /// <summary>Planes de energía: un solo esquema activo (GUID canónico).</summary>
    public static IReadOnlyDictionary<string, string> PowerSchemeTargets { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["maximum-power-plan"] = PowerSchemes.HighPerformanceGuid,
            ["configure-gaming-power-mode-ac"] = PowerSchemes.UltimatePerformanceGuid,
            ["restore-balanced-power-dc"] = PowerSchemes.BalancedGuid,
            ["restore-power-plan-after-gaming"] = PowerSchemes.BalancedGuid,
        };

    public static bool IsPowerScheme(string optimizationId) =>
        PowerSchemeTargets.ContainsKey(optimizationId);

    public enum ConflictOutcome { None, AlreadyApplied, Blocked }

    public sealed record ConflictResult(
        ConflictOutcome Outcome,
        string? ActiveMemberId,
        string MessageEs);

    /// <summary>
    /// Evalúa el plan activo frente al solicitado (GUIDs canónicos, sin
    /// ejecutor: lectura de registro).
    /// </summary>
    public static ConflictResult EvaluatePowerScheme(string requestedId, string? activeGuid)
    {
        if (!PowerSchemeTargets.TryGetValue(requestedId, out var target))
        {
            return new(ConflictOutcome.None, null, string.Empty);
        }
        if (string.IsNullOrWhiteSpace(activeGuid))
        {
            return new(ConflictOutcome.None, null, string.Empty);
        }
        if (string.Equals(activeGuid.Trim(), target, StringComparison.OrdinalIgnoreCase))
        {
            return new(ConflictOutcome.AlreadyApplied, requestedId,
                "Ese plan de energía ya está activo — no se puede volver a aplicar.");
        }
        var activeMember = PowerSchemeTargets
            .FirstOrDefault(kv => string.Equals(activeGuid.Trim(), kv.Value, StringComparison.OrdinalIgnoreCase));
        if (activeMember.Key is null)
        {
            // Plan ajeno a CA-O (p. ej. Economizador): cambiar es legítimo.
            return new(ConflictOutcome.None, null, string.Empty);
        }
        return new(ConflictOutcome.Blocked, activeMember.Key,
            $"Conflicto: el plan '{activeMember.Key}' ya está activo. " +
            $"Revierte '{activeMember.Key}' antes de activar '{requestedId}': " +
            "dos planes no pueden estar activos a la vez.");
    }

    /// <summary>Miembro del grupo ya aplicado según recomendaciones vigentes.</summary>
    public static string? FindAppliedSibling(
        string optimizationId,
        IEnumerable<(string Id, OptimizationState State)> siblings)
    {
        if (!IsPowerScheme(optimizationId))
        {
            return null;
        }
        foreach (var (id, state) in siblings)
        {
            if (!id.Equals(optimizationId, StringComparison.OrdinalIgnoreCase) &&
                IsPowerScheme(id) &&
                state == OptimizationState.AppliedByCao)
            {
                return id;
            }
        }
        return null;
    }
}
