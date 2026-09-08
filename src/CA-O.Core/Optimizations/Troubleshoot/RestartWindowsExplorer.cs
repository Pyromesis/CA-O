using System.Diagnostics;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Barra de tareas o escritorio congelados: reinicia el Explorador (vuelve solo).</summary>
public sealed class RestartWindowsExplorer : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "restart-windows-explorer",
        NameEs = "Reiniciar Explorador de Windows",
        NameEn = "Restart Windows Explorer",
        DescriptionEs = "Reinicia el Explorador cuando la barra o el escritorio se congelan. Vuelve solo.",
        DescriptionEn = "Restarts Explorer when the taskbar or desktop freezes. It relaunches itself.",
        TooltipEs = "Termina explorer.exe; Windows lo relanza. La pantalla parpadea unos segundos.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.TaskKillExplorer, ["/F", "/IM", "explorer.exe"], ct);
        if (!result.Success)
            return OperationResult.Fail("No se pudo reiniciar el Explorador.", result.StdErr);

        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        return OperationResult.Ok("Explorador reiniciado.");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("El reinicio no requiere reversión."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        try
        {
            var running = Process.GetProcessesByName("explorer").Length > 0;
            return Task.FromResult(running
                ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Explorador en ejecución tras el reinicio.")
                : VerificationResult.Failed(OptimizationState.NotApplied, "El Explorador no volvió a arrancar."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown, ex.Message));
        }
    }
}
