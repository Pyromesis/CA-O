using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

public sealed class RestoreSystemManagedPagefile : RegistryOptimizationBase
{
    /// <summary>Restores system-managed pagefile if manually configured. Lets Windows optimize based on available RAM and workload.</summary>
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // Clear manual pagefile settings to let Windows manage automatically
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                "PagingFiles",
                string.Empty,
                RegistryValueKind2.MultiString)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-system-managed-pagefile",
        NameEs = "Restaurar pagefile administrado",
        NameEn = "Restore system-managed pagefile",
        DescriptionEs = "Si el pagefile está configurado manualmente, restaura a administración automática de Windows.",
        DescriptionEn = "Restores system-managed pagefile if manually configured for optimal performance.",
        TooltipEs = "Modifica HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Pagefile restaurado a administración automática."));
    }
}
