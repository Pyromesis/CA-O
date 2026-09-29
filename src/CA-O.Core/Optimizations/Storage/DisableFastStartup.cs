using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Desactiva el inicio rápido (HiberbootEnabled=0): Windows hace apagado
/// completo en vez de hibernación parcial del kernel. Complementa sin
/// duplicar a disable-hibernate: aquello elimina hiberfil.sys (varios GB);
/// esto solo cambia el modo de apagado/arranque. Arranques en SSD apenas
/// lo notan; evita estados raros tras actualizar y en dual-boot.
/// </summary>
public sealed class DisableFastStartup : RegistryOptimizationBase
{
    internal const string PowerKeyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";

    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[] { new ValueTarget(RegistryHive2.LocalMachine, PowerKeyPath, "HiberbootEnabled", 0) };

    public override OptimizationDefinition Definition => new()
    {
        Id = "disable-fast-startup",
        NameEs = "Desactivar inicio rápido",
        NameEn = "Disable fast startup",
        DescriptionEs = "Apagado completo en vez de hibernación parcial del kernel (arranques más limpios).",
        DescriptionEn = "Full shutdown instead of partial kernel hibernation (cleaner boots).",
        TooltipEs = "HiberbootEnabled=0. No duplica a disable-hibernate: aquello libera hiberfil.sys; esto solo desactiva el arranque rápido. Reversible via snapshot.",
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
        return Task.FromResult(OperationResult.Ok("Inicio rápido desactivado (apagado completo)."));
    }
}
