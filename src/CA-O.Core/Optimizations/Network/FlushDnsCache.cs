using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Network;

/// <summary>Flushes the DNS resolver cache via ipconfig. Idempotent, no rollback needed.</summary>
public sealed class FlushDnsCache : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "flush-dns-cache",
        NameEs = "Vaciar cache DNS",
        NameEn = "Flush DNS cache",
        DescriptionEs = "Mantenimiento menor, sin impacto en juegos. Util para resolucion DNS inconsistente.",
        DescriptionEn = "Minor maintenance, no gaming impact. Useful for inconsistent DNS resolution.",
        TooltipEs = "Ejecuta ipconfig /flushdns. Idempotente: no requiere reversión.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Tiny,
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
            SystemCommandKey.IpConfigFlushDns, ["/flushdns"], ct);

        return result.Success
            ? OperationResult.Ok("Caché DNS vaciada correctamente.")
            : OperationResult.Fail("No se pudo vaciar la caché DNS.", result.StdErr);
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("Vaciar caché no requiere reversión."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default) =>
        Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao, "Comando de vaciado ejecutado con éxito."));

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Sin precondiciones específicas."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Command",
                    Target = "ipconfig /flushdns",
                    Before = "caché DNS actual",
                    After = "caché vacía",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
