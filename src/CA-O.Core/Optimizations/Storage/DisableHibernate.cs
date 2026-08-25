using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>#9: powercfg /h off — frees hiberfil.sys GBs.</summary>
public sealed class DisableHibernate : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar hibernación",
        NameEn = "Disable hibernation",
        DescriptionEs = "Elimina hiberfil.sys y libera varios GB en disco.",
        DescriptionEn = "Removes hiberfil.sys and frees several GB of disk space.",
        TooltipEs = "Pierdes la hibernación (Inicio rápido usa parte de ella). El archivo se elimina al desactivar.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.NoKnownConflict,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    private static string Id => "disable-hibernate";

    /// <summary>Hibernation availability injected from `powercfg /a`.</summary>
    public bool HibernateAvailable { get; set; } = true;

    public OptimizationState Detect(IRegistryAccessor registry) =>
        HibernateAvailable ? OptimizationState.NotApplied : OptimizationState.AppliedByCao;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"hibernate={(HibernateAvailable ? "available" : "off")}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Process is null) return OperationResult.Fail("Runner no disponible.", "IProcessRunner null");
        var (code, output) = await context.Process.RunAsync("powercfg", "/h off", ct);
        return code == 0
            ? OperationResult.Ok("Hibernación desactivada; hiberfil.sys liberado.")
            : OperationResult.Fail("No se pudo desactivar la hibernación.", output);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Process is null) return OperationResult.Fail("Runner no disponible.", "IProcessRunner null");
        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("hibernate=", StringComparison.Ordinal));
        if (note == "hibernate=off") return OperationResult.Ok("Ya estaba desactivada antes; nada que restaurar.");

        var (code, output) = await context.Process.RunAsync("powercfg", "/h on", ct);
        return code == 0
            ? OperationResult.Ok("Hibernación reactivada.")
            : OperationResult.Fail("No se pudo reactivar la hibernación.", output);
    }
}
