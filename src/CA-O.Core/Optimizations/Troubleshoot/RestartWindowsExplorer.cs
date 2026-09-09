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
        // Nunca lanza: cada paso captura su error con contexto para que la UI
        // muestre la causa real en vez del genérico "Error inesperado".
        try
        {
            if (context.Executor is null)
                return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

            bool killed;
            string killDetail;
            try
            {
                var result = await context.Executor.ExecuteAsync(
                    SystemCommandKey.TaskKillExplorer, ["/F", "/IM", "explorer.exe"], ct);
                killed = result.Success;
                killDetail = result.StdErr;
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(
                    $"No se pudo detener el Explorador ({ex.GetType().Name}: {ex.Message}). {ExplorerShell.RecoveryHintEs}",
                    "kill-failed");
            }
            if (!killed)
                return OperationResult.Fail(
                    $"No se pudo detener el Explorador ({killDetail}). {ExplorerShell.RecoveryHintEs}",
                    "kill-failed");

            // Esperar a que salga del todo antes de relanzar: si sigue vivo,
            // relanzar crearía un segundo shell en vez del escritorio.
            if (!await ExplorerShell.WaitForExitAsync(ct))
                return OperationResult.Fail(
                    "El Explorador no terminó de cerrarse; espera unos segundos y reintenta. " + ExplorerShell.RecoveryHintEs,
                    "exit-timeout");

            // Relanzar explícitamente en la sesión interactiva (ver ExplorerShell:
            // el servicio vive en sesión 0 y esperar no basta).
            var (ok, error) = await ExplorerShell.EnsureInteractiveExplorerAsync(ct);
            if (!ok)
                return OperationResult.Fail($"Se detuvo explorer.exe pero el escritorio no volvió. {error}", "relaunch-failed");
            return OperationResult.Ok("Explorador reiniciado.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(
                $"Fallo interno reiniciando el Explorador ({ex.GetType().Name}: {ex.Message}). {ExplorerShell.RecoveryHintEs}",
                "unexpected");
        }
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("El reinicio no requiere reversión."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        try
        {
            // Exigir shell INTERACTIVO (sesión > 0): una copia en sesión 0
            // (servicios) no es un escritorio válido y antes daba falso éxito.
            return Task.FromResult(ExplorerShell.AnyInteractiveExplorer()
                ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Explorador en ejecución tras el reinicio.")
                : VerificationResult.Failed(OptimizationState.NotApplied, "El Explorador no volvió a arrancar."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown, ex.Message));
        }
    }
}
