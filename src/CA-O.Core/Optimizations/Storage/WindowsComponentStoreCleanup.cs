using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Runs DISM component store cleanup (StartComponentCleanup).</summary>
public sealed class WindowsComponentStoreCleanup : IOptimization
{
    private int? _lastExitCode;

    public OptimizationDefinition Definition => new()
    {
        Id = "windows-component-store-cleanup",
        NameEs = "Limpieza Component Store",
        NameEn = "Windows Component Store cleanup",
        DescriptionEs = "Ejecuta DISM para limpiar componentes reemplazados de WinSxS. Puede tardar varios minutos.",
        DescriptionEn = "Runs DISM to clean superseded WinSxS components. May take several minutes.",
        TooltipEs = "Ejecuta DISM /Online /Cleanup-Image /StartComponentCleanup. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
        Flags = OptimizationFlags.NotReversible,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.DismStartComponentCleanup,
            ["/Online", "/Cleanup-Image", "/StartComponentCleanup"], ct);
        _lastExitCode = result.ExitCode;

        return result.Success
            ? OperationResult.Ok("Limpieza del Component Store completada.")
            : OperationResult.Fail("DISM StartComponentCleanup falló.", result.StdErr);
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La limpieza del Component Store no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastExitCode is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución de DISM."));
        }

        return Task.FromResult(_lastExitCode == 0
            ? VerificationResult.Passed(OptimizationState.Unknown, "DISM ejecutado con exit=0.")
            : VerificationResult.Failed(OptimizationState.Unknown, $"DISM terminó con exit={_lastExitCode}."));
    }
}
