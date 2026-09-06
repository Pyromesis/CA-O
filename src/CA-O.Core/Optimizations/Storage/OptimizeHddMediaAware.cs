using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Runs media-aware optimization on C: (defrag HDD / ReTrim SSD).</summary>
public sealed class OptimizeHddMediaAware : IOptimization
{
    private int? _lastExitCode;

    public OptimizationDefinition Definition => new()
    {
        Id = "optimize-hdd-media-aware",
        NameEs = "Optimizar HDD media-aware",
        NameEn = "Optimize HDD media-aware",
        DescriptionEs = "Ejecuta la optimización adecuada al medio en C:: desfragmenta HDD y hace ReTrim en SSD.",
        DescriptionEn = "Runs the media-appropriate optimization on C:: defrags HDDs, re-trims SSDs.",
        TooltipEs = "Ejecuta defrag C: /O (elige según el tipo real de disco). Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.DefragC, ["C:", "/O"], ct);
        _lastExitCode = result.ExitCode;

        return result.Success
            ? OperationResult.Ok("Optimización media-aware completada.")
            : OperationResult.Fail("defrag /O falló.", result.StdErr);
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La optimización de disco no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastExitCode is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución de defrag /O."));
        }

        return Task.FromResult(_lastExitCode == 0
            ? VerificationResult.Passed(OptimizationState.Unknown, "defrag /O ejecutado con exit=0.")
            : VerificationResult.Failed(OptimizationState.Unknown, $"defrag /O terminó con exit={_lastExitCode}."));
    }
}
