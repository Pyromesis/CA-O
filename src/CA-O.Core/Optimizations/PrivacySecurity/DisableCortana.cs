using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.PrivacySecurity;

/// <summary>#14: AllowCortana=0 (policy).</summary>
public sealed class DisableCortana : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar Cortana",
        NameEn = "Disable Cortana",
        DescriptionEs = "Desactiva el asistente Cortana mediante directiva.",
        DescriptionEn = "Disables the Cortana assistant via policy.",
        TooltipEs = "AllowCortana=0. En Windows 11 moderno Cortana ya está retirada; el valor no hace daño si no existe.",
        Category = OptimizationCategory.PrivacySecurity,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.NoKnownConflict,
        SecurityImpact = SecurityImpact.PrivacyOnly,
        Impact = ImpactLevel.Low,
    };

    private static string Id => "disable-cortana";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Cortana desactivada por directiva."));
    }
}
