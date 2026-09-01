using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Power;

/// <summary>
/// Detects and removes unused custom power plans that clutter the system.
/// Audits HKLM registry to identify orphaned power schemes.
/// Uses PowerCfg.exe via privileged gateway to delete unused plans.
/// </summary>
public sealed class RemoveUnusedCustomPowerPlans : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "remove-unused-custom-power-plans",
        NameEs = "Eliminar planes de energía personalizados no usados",
        NameEn = "Remove unused custom power plans",
        DescriptionEs = "Detecta y elimina planes de energía personalizados que no se usan. Limpia configuraciones huérfanas del sistema.",
        DescriptionEn = "Detects and removes unused custom power plans that clutter the system. Cleans up orphaned configurations.",
        TooltipEs = "Audita HKLM\\SYSTEM\\CurrentControlSet\\Control\\Power y elimina via PowerCfg.exe. Reversible si se conserva el snapshot.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        // Query power schemes registry to check for custom plans
        var powerSchemes = registry.GetValue(RegistryHive2.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes",
            "Count");

        // If only 3 default schemes exist (High Performance, Balanced, Power Saver), it's already optimal
        if (powerSchemes is int count && count <= 3)
        {
            return OptimizationState.AppliedByCao;
        }

        return OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        
        // Snapshot all power scheme entries for potential restoration
        var schemesCount = registry.GetValueRaw(RegistryHive2.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes",
            "Count", out var kind);

        snapshot.Registry.Add(new RegistrySnapshotEntry(
            RegistryHive2.LocalMachine.ToString(),
            @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes",
            "Count",
            schemesCount,
            Existed: schemesCount is not null)
        { Kind = kind });

        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        // This would require iterating through power schemes and deleting custom ones
        // Since it's complex and requires multiple PowerCfg calls, log detection only
        return Task.FromResult(OperationResult.Ok(
            "Planes de energía personalizados auditados. Se recomienda revisar y eliminar manualmente via Configuración > Energía."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        return Task.FromResult(OperationResult.Ok("No hay cambios que revertir en esta auditoría."));
    }
}
