using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Ensures microphone privacy consent is set to Allow.</summary>
public sealed class FixMicrophoneAccess : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", "Value", "Allow", RegistryValueKind2.String) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "fix-microphone-access",
        NameEs = "Permitir acceso al micrófono",
        NameEn = "Allow microphone access",
        DescriptionEs = "Activa el permiso de micrófono a nivel de sistema cuando las apps no lo detectan.",
        DescriptionEn = "Enables system-wide microphone consent when apps cannot detect it.",
        TooltipEs = "Establece Value=Allow en el almacén de consentimiento del micrófono. Reversible exacto.",
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

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Acceso al micrófono permitido."));
    }
}
