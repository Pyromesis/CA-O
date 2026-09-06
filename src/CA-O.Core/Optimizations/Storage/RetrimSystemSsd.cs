using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Runs an immediate ReTrim on the system drive (defrag C: /L).</summary>
public sealed class RetrimSystemSsd : IOptimization
{
    private int? _lastExitCode;

    public OptimizationDefinition Definition => new()
    {
        Id = "retrim-system-ssd",
        NameEs = "Retrim del SSD del sistema",
        NameEn = "Retrim system SSD",
        DescriptionEs = "Ejecuta ReTrim inmediato en la unidad C: para liberar bloques no usados del SSD.",
        DescriptionEn = "Runs an immediate ReTrim on drive C: to free unused SSD blocks.",
        TooltipEs = "Ejecuta defrag C: /L. Acción de mantenimiento no reversible; puede tardar.",
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
            SystemCommandKey.DefragRetrim, ["C:", "/L"], ct);
        _lastExitCode = result.ExitCode;

        return result.Success
            ? OperationResult.Ok("ReTrim del SSD completado.")
            : OperationResult.Fail("defrag /L falló.", result.StdErr);
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("El ReTrim no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastExitCode is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución de defrag /L."));
        }

        return Task.FromResult(_lastExitCode == 0
            ? VerificationResult.Passed(OptimizationState.Unknown, "defrag /L ejecutado con exit=0.")
            : VerificationResult.Failed(OptimizationState.Unknown, $"defrag /L terminó con exit={_lastExitCode}."));
    }
}
