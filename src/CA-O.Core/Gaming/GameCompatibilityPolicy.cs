using CAO.Shared;

namespace CAO.Core.Gaming;

public enum GameCompatibility { Safe, Caution, Blocked, SecuritySensitive, Experimental }

public static class GameCompatibilityPolicy
{
    // Matriz documentada: optimización vs juego/anti-cheat
    private static readonly IReadOnlySet<string> VbsRelated = new HashSet<string>(StringComparer.Ordinal)
    {
        "disable-vbs", "disable-hvci", "disable-memory-integrity", "hypervisor-launchtype-off"
    };

    private static readonly IReadOnlySet<string> SecuritySensitive = new HashSet<string>(StringComparer.Ordinal)
    {
        "disable-vbs", "disable-hvci", "disable-memory-integrity", "hypervisor-launchtype-off",
        "disable-tpm", "disable-secure-boot-check"
    };

    private static readonly IReadOnlySet<string> SafeForGaming = new HashSet<string>(StringComparer.Ordinal)
    {
        "disable-transparency", "disable-game-bar", "power-plan-high-performance", "gpu-scheduling"
    };

    public static (GameCompatibility Compatibility, string ReasonEs) Evaluate(string optimizationId, SystemContext context)
    {
        // Vanguard kernel-level: VBS/HVCI bloqueado absoluto
        if (context.VanguardDetected && VbsRelated.Contains(optimizationId))
            return (GameCompatibility.Blocked, "Bloqueado: Vanguard exige VBS/HVCI activo. Desactivarlo puede impedir iniciar Valorant.");

        // Cualquier anti-cheat + optimización reduce protección => bloqueado
        if (context.AntiCheats.Count > 0 && SecuritySensitive.Contains(optimizationId))
            return (GameCompatibility.Blocked, $"Bloqueado: anti-cheat detectado ({string.Join(", ", context.AntiCheats.Select(a => a.Kind))}) — modificación reduce protección.");

        // SAFE list
        if (SafeForGaming.Contains(optimizationId))
            return (GameCompatibility.Safe, "Permitida en contexto gaming.");

        // Si hay anti-cheat y la optimización es de riesgo High/Critical en categoría sensible
        // Se delega a AntiCheatGuard pero aquí marcamos Caution
        if (context.AntiCheats.Count > 0)
            return (GameCompatibility.Caution, "Requiere revisión: anti-cheat presente, verificar impacto.");

        return (GameCompatibility.Safe, "Sin conflicto gaming detectado.");
    }

    public static bool IsBlocked(string optimizationId, SystemContext context) =>
        Evaluate(optimizationId, context).Compatibility == GameCompatibility.Blocked;

    public static IReadOnlyList<string> GetBlockedOptimizations(SystemContext context) =>
        VbsRelated.Where(id => IsBlocked(id, context)).ToList();

    public static IReadOnlyList<string> GetAllowedOptimizations(SystemContext context, IEnumerable<string> candidates) =>
        candidates.Where(id => !IsBlocked(id, context)).ToList();
}
