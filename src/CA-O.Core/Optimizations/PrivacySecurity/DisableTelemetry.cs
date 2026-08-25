using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.PrivacySecurity;

/// <summary>#13: AllowTelemetry=0 (policy).</summary>
public sealed class DisableTelemetry : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar telemetría",
        NameEn = "Disable telemetry",
        DescriptionEs = "Fija la directiva de telemetría al mínimo permitido por la edición de Windows.",
        DescriptionEn = "Sets the diagnostic-data policy to the minimum allowed by the Windows edition.",
        TooltipEs = "AllowTelemetry=0 vía directiva. En Home el mínimo efectivo es 'Requerido básico'. No toca servicios: eso sería una decisión aparte.",
        Category = OptimizationCategory.PrivacySecurity,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.PrivacyOnly,
        Impact = ImpactLevel.Medium,
    };

    private static string Id => "disable-telemetry";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Directiva de telemetría al mínimo."));
    }
}
