using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Resynchronizes the system clock (w32tm /resync).</summary>
public sealed class ResyncSystemClock : IOptimization
{
    private int? _lastExitCode;

    public OptimizationDefinition Definition => new()
    {
        Id = "resync-system-clock",
        NameEs = "Resincronizar reloj del sistema",
        NameEn = "Resync system clock",
        DescriptionEs = "Sincroniza el reloj con el servidor horario. Corrige desfases de hora.",
        DescriptionEn = "Syncs the clock with the time server. Fixes clock drift.",
        TooltipEs = "Ejecuta w32tm /resync. Mantenimiento no reversible.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.W32tmResync, ["/resync"], ct);
        _lastExitCode = result.ExitCode;

        return result.Success
            ? OperationResult.Ok("Reloj sincronizado correctamente.")
            : OperationResult.Fail("No se pudo sincronizar el reloj.", result.StdErr);
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La sincronización no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastExitCode is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }

        return Task.FromResult(_lastExitCode == 0
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "w32tm ejecutado con exit=0.")
            : VerificationResult.Failed(OptimizationState.NotApplied, $"w32tm terminó con exit={_lastExitCode}."));
    }
}
