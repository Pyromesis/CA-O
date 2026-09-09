using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>
/// Sin barra ni escritorio: trae de vuelta el Explorador sin matar nada.
/// Solo lanza el shell si falta; si ya está, éxito inmediato (idempotente).
/// </summary>
public sealed class RecoverWindowsExplorer : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "recover-windows-explorer",
        NameEs = "Recuperar Explorador de Windows",
        NameEn = "Recover Windows Explorer",
        DescriptionEs = "Restaura la barra y el escritorio si desaparecieron. Solo actúa si falta.",
        DescriptionEn = "Restores the taskbar and desktop if they are gone. Only acts when missing.",
        TooltipEs = "Lanza explorer.exe en tu sesión si no hay shell. Seguro y repetible.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible,
    };

    public OptimizationState Detect(IRegistryAccessor registry) =>
        ExplorerShell.AnyInteractiveExplorer() ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        try
        {
            if (ExplorerShell.AnyInteractiveExplorer())
                return OperationResult.Ok("El Explorador ya está en ejecución; nada que recuperar.");
            var (ok, error) = await ExplorerShell.EnsureInteractiveExplorerAsync(ct);
            return ok
                ? OperationResult.Ok("Explorador recuperado: barra y escritorio de vuelta.")
                : OperationResult.Fail(error, "relaunch-failed");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(
                $"Fallo interno recuperando el Explorador ({ex.GetType().Name}: {ex.Message}). {ExplorerShell.RecoveryHintEs}",
                "unexpected");
        }
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La recuperación no requiere reversión."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default) =>
        Task.FromResult(ExplorerShell.AnyInteractiveExplorer()
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Explorador en ejecución.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "El Explorador sigue sin arrancar."));
}
