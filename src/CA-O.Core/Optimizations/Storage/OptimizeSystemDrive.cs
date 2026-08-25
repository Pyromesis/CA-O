using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// #19: Optimize-Volume on the system drive. HDD -> -Defrag, SSD -> -ReTrim
/// (the spec's single command mixed both; the correct flag depends on media).
/// Maintenance action with coarse progress, not reversible by design.
/// </summary>
public sealed class OptimizeSystemDrive : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Optimizar disco del sistema",
        NameEn = "Optimize system drive",
        DescriptionEs = "Desfragmenta (HDD) o hace ReTrim (SSD) de la unidad C: según su tipo real.",
        DescriptionEn = "Defrags (HDD) or re-trims (SSD) the C: drive based on its actual media type.",
        TooltipEs = "Ejecuta Optimize-Volume con la opción correcta para tu disco. Acción de mantenimiento; puede tardar minutos.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
        Flags = OptimizationFlags.NotReversible,
    };

    private static string Id => "optimize-system-drive";

    /// <summary>Injected by the engine from MSFT_PhysicalDisk ("SSD"/"HDD"/"Unspecified").</summary>
    public string SystemDiskMediaType { get; set; } = "SSD";

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Process is null) return OperationResult.Fail("Runner no disponible.", "IProcessRunner null");
        var option = string.Equals(SystemDiskMediaType, "HDD", StringComparison.OrdinalIgnoreCase) ? "-Defrag" : "-ReTrim";
        var (code, output) = await context.Process.RunAsync(
            "powershell", $"-NoProfile -NonInteractive -Command \"Optimize-Volume -DriveLetter C {option}\"", ct);
        return code == 0
            ? OperationResult.Ok($"Optimización ({option}) completada.")
            : OperationResult.Fail("Optimize-Volume falló.", output);
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La optimización de disco no se revierte (mantenimiento)."));
}
