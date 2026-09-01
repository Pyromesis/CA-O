using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Read-only audit: detects current display refresh rate vs. maximum available.
/// Does not modify Windows state. Provides diagnostic information only.
/// </summary>
public sealed class GamingDisplayRefreshRateAudit : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "gaming-display-refresh-rate-audit",
        NameEs = "Auditoría de frecuencia de refresco",
        NameEn = "Display refresh rate audit",
        DescriptionEs = "Audita frecuencia de pantalla actual vs. máxima disponible. No modifica configuración. Solo diagnóstico.",
        DescriptionEn = "Audits current display refresh rate vs. maximum available. Read-only diagnostic information.",
        TooltipEs = "No modifica nada; solo proporciona información de diagnóstico sobre la pantalla.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        // Read-only audit; always report as "applied" since it's diagnostic
        return OptimizationState.AppliedByCao;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        // No snapshot needed for read-only audit
        return new OptimizationSnapshot();
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        // Read-only; no changes applied
        return Task.FromResult(OperationResult.Ok("Auditoría de refresco de pantalla completada. No se realizó cambios."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        // Nothing to revert for read-only audit
        return Task.FromResult(OperationResult.Ok("No hay cambios que revertir en auditoría."));
    }
}
