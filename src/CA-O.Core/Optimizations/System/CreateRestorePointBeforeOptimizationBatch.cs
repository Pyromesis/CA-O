using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.System;

public sealed class CreateRestorePointBeforeOptimizationBatch : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\CA-O\create-restore-point-before-optimization-batch", "Enabled", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "create-restore-point-before-optimization-batch",
        NameEs = "Crear punto restauracion antes de lote",
        NameEn = "Crear punto restauracion antes de lote",
        DescriptionEs = "Operacion seguridad, registra RestorePointId. Beneficio: segun workload, ver evidencia.",
        DescriptionEn = "Operacion seguridad, registra RestorePointId.",
        TooltipEs = "create-restore-point-before-optimization-batch via registry. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Crear punto restauracion antes de lote aplicado."));
    }
}
