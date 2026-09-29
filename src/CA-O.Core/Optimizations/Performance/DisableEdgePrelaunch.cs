using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Performance;

/// <summary>
/// Desactiva el pre-lanzamiento de Edge por directiva (AllowPrelaunch=0):
/// Edge deja de precargarse en segundo plano al iniciar sesión, lo que
/// recorta RAM y procesos en reposo. No desinstala Edge ni cambia el
/// navegador predeterminado. Reversible via snapshot.
/// </summary>
public sealed class DisableEdgePrelaunch : RegistryOptimizationBase
{
    internal const string EdgeMainPolicyKey = @"SOFTWARE\Policies\Microsoft\MicrosoftEdge\Main";

    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.LocalMachine, EdgeMainPolicyKey, "AllowPrelaunch", 0) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-edge-prelaunch",
        NameEs = "Desactivar pre-lanzamiento de Edge",
        NameEn = "Disable Edge prelaunch",
        DescriptionEs = "Evita que Edge se precargue al iniciar sesión (menos RAM en reposo).",
        DescriptionEn = "Stops Edge from preloading at sign-in (less idle RAM).",
        TooltipEs = "AllowPrelaunch=0 por directiva. No desinstala Edge ni cambia el navegador predeterminado. Reversible via snapshot.",
        Category = OptimizationCategory.Performance,
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
        return Task.FromResult(OperationResult.Ok("Pre-lanzamiento de Edge desactivado."));
    }
}
