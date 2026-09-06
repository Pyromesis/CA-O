using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Restores the system-managed pagefile (AutomaticManagedPagefile=1).</summary>
public sealed class RestoreSystemManagedPagefile : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            new ValueTarget(
                RegistryHive2.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                "AutomaticManagedPagefile",
                1,
                RegistryValueKind2.DWord)
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "restore-system-managed-pagefile",
        NameEs = "Restaurar pagefile administrado",
        NameEn = "Restore system-managed pagefile",
        DescriptionEs = "Devuelve el archivo de paginación a gestión automática de Windows.",
        DescriptionEn = "Returns the pagefile to Windows automatic management.",
        TooltipEs = "Establece AutomaticManagedPagefile=1. Requiere reinicio. Reversible exacto.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.RequiresReboot,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Pagefile devuelto a gestión automática. Reinicie para aplicar."));
    }
}
