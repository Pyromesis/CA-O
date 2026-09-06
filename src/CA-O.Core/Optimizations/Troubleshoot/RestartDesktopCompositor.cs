using System.Diagnostics;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Restarts the Desktop Window Manager (Windows recomposes automatically).</summary>
public sealed class RestartDesktopCompositor : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "restart-desktop-compositor",
        NameEs = "Reiniciar compositor de escritorio",
        NameEn = "Restart desktop compositor",
        DescriptionEs = "Reinicia el compositor (DWM) para parpadeos o pantalla en negro. Windows lo recompone solo.",
        DescriptionEn = "Restarts the compositor (DWM) for flicker or black screen. Windows recomposes automatically.",
        TooltipEs = "Termina dwm.exe; el sistema lo reinicia. La pantalla parpadea unos segundos.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
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
            SystemCommandKey.TaskKillDwm, ["/F", "/IM", "dwm.exe"], ct);
        if (!result.Success)
            return OperationResult.Fail("No se pudo reiniciar el compositor.", result.StdErr);

        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        return OperationResult.Ok("Compositor reiniciado.");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("El reinicio no requiere reversión."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        try
        {
            var running = Process.GetProcessesByName("dwm").Length > 0;
            return Task.FromResult(running
                ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Compositor en ejecución tras el reinicio.")
                : VerificationResult.Failed(OptimizationState.NotApplied, "El compositor no volvió a arrancar."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown, ex.Message));
        }
    }
}
