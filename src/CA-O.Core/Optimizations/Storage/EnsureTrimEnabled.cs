using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Ensures TRIM is enabled (fsutil DisableDeleteNotify 0).</summary>
public sealed class EnsureTrimEnabled : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "ensure-trim-enabled",
        NameEs = "Asegurar TRIM habilitado en SSD",
        NameEn = "Ensure TRIM enabled on SSD",
        DescriptionEs = "Activa TRIM con fsutil para mantener el rendimiento del SSD al liberar bloques.",
        DescriptionEn = "Enables TRIM via fsutil to keep SSD performance when freeing blocks.",
        TooltipEs = "Ejecuta fsutil behavior set DisableDeleteNotify 0. Reversible exacto.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.FsutilDisableDeleteNotifyOff,
            ["behavior", "set", "DisableDeleteNotify", "0"], ct);

        return result.Success
            ? OperationResult.Ok("TRIM habilitado (DisableDeleteNotify 0).")
            : OperationResult.Fail("No se pudo habilitar TRIM.", result.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.FsutilDisableDeleteNotifyOn,
            ["behavior", "set", "DisableDeleteNotify", "1"], ct);

        return result.Success
            ? OperationResult.Ok("TRIM devuelto a deshabilitado.")
            : OperationResult.Fail("No se pudo revertir TRIM.", result.StdErr);
    }

    public async Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return VerificationResult.Unknown(OptimizationState.Unknown, "Ejecutor no disponible.");

        var query = await context.Executor.ExecuteAsync(
            SystemCommandKey.FsutilQueryDeleteNotify,
            ["behavior", "query", "DisableDeleteNotify"], ct);
        if (!query.Success)
            return VerificationResult.Unknown(OptimizationState.Unknown, "No se pudo consultar TRIM: " + query.StdErr);

        return query.StdOut.Contains('0')
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "TRIM verificado habilitado.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "TRIM no está habilitado tras aplicar.");
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Requiere SSD."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Command",
                    Target = "fsutil behavior set DisableDeleteNotify 0",
                    Before = "desconocido",
                    After = "DisableDeleteNotify = 0",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
