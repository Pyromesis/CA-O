using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

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
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
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
        if (context.Executor is null)
        {
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");
        }

        var key = string.Equals(SystemDiskMediaType, "HDD", StringComparison.OrdinalIgnoreCase)
            ? SystemCommandKey.OptimizeVolumeDefragC
            : SystemCommandKey.OptimizeVolumeReTrimC;

        var result = await context.Executor.ExecuteAsync(key,
        [
            "-NoProfile", "-NonInteractive", "-Command",
            key == SystemCommandKey.OptimizeVolumeDefragC
                ? "Optimize-Volume -DriveLetter C -Defrag"
                : "Optimize-Volume -DriveLetter C -ReTrim",
        ], ct);

        _lastApplyExitCode = result.ExitCode;
        LastOperationExecuted = key == SystemCommandKey.OptimizeVolumeDefragC ? "-Defrag" : "-ReTrim";

        return result.Success
            ? OperationResult.Ok("Optimización de disco completada.")
            : OperationResult.Fail("Optimize-Volume falló.", result.StdErr);
    }

    /// <summary>
    /// P0-5: irreversible != unverifiable. Verification is based on the
    /// execution evidence captured during Apply (exit code + operation
    /// performed against the expected volume), not on registry state.
    /// </summary>
    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastApplyExitCode is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "No hay evidencia de ejecución de Optimize-Volume."));
        }

        return _lastApplyExitCode == 0
            ? Task.FromResult(VerificationResult.Passed(OptimizationState.Unknown,
                $"Optimize-Volume ({LastOperationExecuted}) ejecutado con exit=0."))
            : Task.FromResult(VerificationResult.Failed(OptimizationState.Unknown,
                $"Optimize-Volume terminó con exit={_lastApplyExitCode}."));
    }

    private int? _lastApplyExitCode;
    private string? LastOperationExecuted;

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La optimización de disco no se revierte (mantenimiento)."));
}
