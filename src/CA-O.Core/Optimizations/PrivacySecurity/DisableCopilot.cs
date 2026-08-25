using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.PrivacySecurity;

/// <summary>#17: TurnOffWindowsCopilot=1 (documented policy value).</summary>
public sealed class DisableCopilot : RegistryOptimizationBase
{
    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1) };

    public override OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar Copilot",
        NameEn = "Disable Copilot",
        DescriptionEs = "Apaga Copilot mediante la directiva oficial de Microsoft.",
        DescriptionEn = "Turns off Copilot using Microsoft's official policy.",
        TooltipEs = "Usa la clave de directivas documentada (TurnOffWindowsCopilot=1), no hacks no documentados.",
        Category = OptimizationCategory.PrivacySecurity,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.NoKnownConflict,
        SecurityImpact = SecurityImpact.PrivacyOnly,
        Impact = ImpactLevel.Low,
    };

    private static string Id => "disable-copilot";

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Copilot desactivado por directiva."));
    }
}
