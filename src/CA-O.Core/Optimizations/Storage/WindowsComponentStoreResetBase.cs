using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Runs DISM ResetBase. Expert-only, irreversible, removes update uninstall data.</summary>
public sealed class WindowsComponentStoreResetBase : IOptimization
{
    private int? _lastExitCode;

    public OptimizationDefinition Definition => new()
    {
        Id = "windows-component-store-resetbase",
        NameEs = "ResetBase Component Store",
        NameEn = "ResetBase Component Store",
        DescriptionEs = "Ejecuta DISM ResetBase y elimina la base de reversión de actualizaciones. Solo expertos.",
        DescriptionEn = "Runs DISM ResetBase and removes update rollback data. Experts only.",
        TooltipEs = "Ejecuta DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase. Irreversible: tras esto no se pueden desinstalar actualizaciones.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Moderate,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.High,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible | OptimizationFlags.ExpertOnly,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.DismResetBase,
            ["/Online", "/Cleanup-Image", "/StartComponentCleanup", "/ResetBase"], ct);
        _lastExitCode = result.ExitCode;

        return result.Success
            ? OperationResult.Ok("ResetBase completado. Ya no se podrán desinstalar actualizaciones previas.")
            : OperationResult.Fail("DISM ResetBase falló.", result.StdErr);
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Fail(
            "ResetBase no es reversible.",
            "not-reversible"));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastExitCode is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución de DISM ResetBase."));
        }

        return Task.FromResult(_lastExitCode == 0
            ? VerificationResult.Passed(OptimizationState.Unknown, "DISM ResetBase ejecutado con exit=0.")
            : VerificationResult.Failed(OptimizationState.Unknown, $"DISM ResetBase terminó con exit={_lastExitCode}."));
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Solo Modo Expert."));
}
