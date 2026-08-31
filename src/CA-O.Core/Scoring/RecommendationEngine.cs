using CAO.Core.Abstractions;
using CAO.Core.Gaming;
using CAO.Shared;

namespace CAO.Core.Engine;

/// <summary>
/// Analyze-first recommendation pipeline (spec 13, 17): every optimization is
/// classified into exactly one bucket against a measured SystemContext. There
/// is deliberately no path that applies "everything": callers must present
/// the buckets and let the user apply Recommended explicitly.
/// </summary>
public static class RecommendationEngine
{
    public static IReadOnlyList<Recommendation> BuildAll(
        IEnumerable<IOptimization> catalog,
        IRegistryAccessor registry,
        SystemContext context)
    {
        var recommendations = new List<Recommendation>();
        foreach (var optimization in catalog)
        {
            recommendations.Add(Build(optimization, registry, context));
        }

        return recommendations;
    }

    public static Recommendation Build(
        IOptimization optimization,
        IRegistryAccessor registry,
        SystemContext context)
    {
        var definition = optimization.Definition;
        var currentState = optimization.Detect(registry);
        var guardReason = AntiCheatGuard.Evaluate(definition, context);

        var (bucket, reason) = Classify(definition, context, currentState, guardReason);
        var score = OptimizationScoreCalculator.Compute(definition);

        return new Recommendation
        {
            OptimizationId = definition.Id,
            NameEs = definition.NameEs,
            DescriptionEs = definition.DescriptionEs,
            Category = definition.Category,
            CurrentState = currentState,
            TargetState = bucket == RecommendationBucket.Recommended
                ? OptimizationState.AppliedByCao
                : currentState,
            Bucket = bucket,
            ExpectedImpact = definition.ExpectedImpact,
            Evidence = definition.Evidence,
            Risk = definition.Risk,
            SecurityImpact = definition.SecurityImpact,
            Compatibility = definition.Compatibility,
            RollbackAvailable = definition.Reversible,
            AntiCheatConflictRisk = guardReason.Code.StartsWith("blocked-anticheat", StringComparison.Ordinal),
            Score = score,
            Reason = reason,
            Flags = definition.Flags,
        };
    }

    private static readonly HashSet<string> StubIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "cleanup-delivery-optimization-cache","cleanup-windows-temp","configure-gaming-power-mode-ac",
        "configure-interrupt-moderation-for-low-latency","create-restore-point-before-optimization-batch",
        "delay-safe-third-party-service-start","delivery-optimization-bandwidth-profile",
        "disable-background-game-captures","disable-game-bar-auto-launch","disable-heavy-startup-apps",
        "disable-nic-power-saving-ac","disable-pcie-link-state-power-saving-ac","disable-selected-third-party-background-task",
        "disable-unnecessary-startup-apps","disable-usb-selective-suspend-ac","disk-cleanup-system-files",
        "enable-auto-hdr","enable-rss","enable-storage-sense","enable-vrr","enable-windowed-game-optimizations",
        "ensure-trim-enabled","flush-dns-cache","free-low-storage-space","gaming-display-refresh-rate-audit",
        "optimize-hdd-media-aware","optimize-startup-recovery-state","pending-reboot-maintenance",
        "remove-unused-custom-power-plans","reset-network-stack-repair","restore-balanced-power-dc",
        "restore-default-gpu-preference","restore-large-send-offload","restore-power-plan-after-gaming",
        "restore-sysmain-default","restore-system-managed-pagefile","restore-tcp-checksum-offload",
        "restore-udp-checksum-offload","restore-windows-search-default","restore-windows-tcp-congestion-default",
        "retrim-system-ssd","set-best-performance-ac","set-games-high-performance-gpu",
        "set-wireless-adapter-max-performance-ac","stale-crash-dump-cleanup","storage-sense-recycle-bin-policy",
        "storage-sense-temp-cleanup","windows-component-store-cleanup","windows-component-store-resetbase"
    };

    private static (RecommendationBucket Bucket, RecommendationReason Reason) Classify(
        OptimizationDefinition definition,
        SystemContext context,
        OptimizationState currentState,
        RecommendationReason guardReason)
    {
        // 0) STUB fail-closed: ninguna optimización placeholder puede ser Recommended/Optional
        if (StubIds.Contains(definition.Id))
        {
            return (RecommendationBucket.NotApplicable,
                new RecommendationReason("stub-not-implemented", "No implementada: solo escribe HKCU\\Software\\CA-O. Retirada del catálogo de producción."));
        }

        // 1) Hard anti-cheat / default-block list wins over everything else.
        if (guardReason.Code == "blocked-by-default")
        {
            return (RecommendationBucket.Experimental, guardReason);
        }

        if (guardReason.Code is "blocked-anticheat" or "blocked-anticheat-category")
        {
            return (RecommendationBucket.SecuritySensitive, guardReason);
        }

        // 2) Contextual applicability: wrong build/hardware/thermal state.
        var precondition = Compatibility.Rules.EvaluatePreconditions(definition, context);
        if (!precondition.Passed)
        {
            return (RecommendationBucket.NotApplicable,
                new RecommendationReason("not-applicable", precondition.ReasonEs));
        }

        // 3) Security trade-offs are never presented as performance work.
        if (definition.SecurityImpact == SecurityImpact.ReducedProtection ||
            definition.Flags.HasFlag(OptimizationFlags.SecurityTradeoff))
        {
            return (RecommendationBucket.SecuritySensitive,
                new RecommendationReason("security-sensitive",
                    "Reduce protección de seguridad; requiere decisión explícita del usuario."));
        }

        // 4) Weak evidence cannot be recommended automatically.
        if (definition.Evidence is EvidenceLevel.Unknown or EvidenceLevel.Heuristic)
        {
            return (RecommendationBucket.Experimental,
                new RecommendationReason("weak-evidence",
                    "Sin evidencia moderna suficiente; sólo para experimentación."));
        }

        // 5) Poor compatibility confidence.
        if (definition.Compatibility is CompatibilityStatus.Unknown or
            CompatibilityStatus.PotentialConflict or CompatibilityStatus.Incompatible)
        {
            return (RecommendationBucket.Experimental,
                new RecommendationReason("compatibility-caution",
                    "La compatibilidad no está suficientemente establecida en este contexto."));
        }

        // 6) Already applied: keep, don't re-recommend.
        if (currentState == OptimizationState.AppliedByCao)
        {
            return (RecommendationBucket.Optional,
                new RecommendationReason("already-applied",
                    "Ya aplicado y verificado; se mantiene bajo seguimiento."));
        }

        if (currentState == OptimizationState.PendingReboot)
        {
            return (RecommendationBucket.Optional,
                new RecommendationReason("pending-reboot",
                    "Aplicado; pendiente de reinicio para surtir efecto."));
        }

        // 7) Maintenance actions are offered as optional work, never auto-applied.
        if (definition.Flags.HasFlag(OptimizationFlags.NotReversible))
        {
            return (RecommendationBucket.Optional,
                new RecommendationReason("maintenance",
                    "Acción de mantenimiento irreversible; ejecutar sólo cuando el usuario lo decida."));
        }

        // 8) Solid evidence + low friction => recommended for this machine.
        if (definition.Risk is RiskLevel.Safe or RiskLevel.Low &&
            definition.ExpectedImpact is not PerformanceImpact.None)
        {
            return (RecommendationBucket.Recommended,
                new RecommendationReason("recommended",
                    "Evidencia sólida, riesgo bajo y compatible con este equipo."));
        }

        // 9) Everything else is optional work the user may choose.
        return (RecommendationBucket.Optional,
            new RecommendationReason("optional",
                "Posible beneficio según carga de trabajo; revisar antes de aplicar."));
    }
}
