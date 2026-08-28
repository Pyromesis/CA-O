using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// #19: optimize the system drive via defrag.exe /O. /O performs the correct
/// optimization for each media type automatically (HDD defrag, SSD trim),
/// replacing the earlier PowerShell Optimize-Volume approach with a smaller,
/// allowlisted, non-interpreted executable.
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
        TooltipEs = "Ejecuta defrag C: /O (optimiza según el tipo real de disco: HDD o SSD). Acción de mantenimiento; puede tardar minutos.",
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

        var key = SystemCommandKey.DefragC;

        var result = await context.Executor.ExecuteAsync(key, ["C:", "/O"], ct);

        _lastApplyExitCode = result.ExitCode;
        LastOperationExecuted = "/O";

        return result.Success
            ? OperationResult.Ok("Optimización de disco completada.")
            : OperationResult.Fail("Defrag.exe falló.", result.StdErr);
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
                "No hay evidencia de ejecución de defrag /O."));
        }

        return _lastApplyExitCode == 0
            ? Task.FromResult(VerificationResult.Passed(OptimizationState.Unknown,
                $"defrag ({LastOperationExecuted}) ejecutado con exit=0."))
            : Task.FromResult(VerificationResult.Failed(OptimizationState.Unknown,
                $"defrag terminó con exit={_lastApplyExitCode}."));
    }

    private int? _lastApplyExitCode;
    private string? LastOperationExecuted;

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La optimización de disco no se revierte (mantenimiento)."));
}
