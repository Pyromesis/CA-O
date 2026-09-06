using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Network;

/// <summary>Repairs Winsock/TCP via netsh. Requires reboot; not directly reversible.</summary>
public sealed class ResetNetworkStackRepair : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "reset-network-stack-repair",
        NameEs = "Reparar pila de red",
        NameEn = "Reset network stack",
        DescriptionEs = "Ejecuta netsh winsock reset y netsh int ip reset. Solo si hay síntomas. Requiere reinicio.",
        DescriptionEn = "Runs netsh winsock reset and netsh int ip reset. Only with symptoms. Requires reboot.",
        TooltipEs = "Reparación real vía netsh. Requiere reinicio y no es reversible salvo punto de restauración.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible | OptimizationFlags.RequiresReboot,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add("reset=winsock+int-ip");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var winsock = await context.Executor.ExecuteAsync(
            SystemCommandKey.NetShWinsockReset, ["winsock", "reset"], ct);
        if (!winsock.Success)
            return OperationResult.Fail("Falló netsh winsock reset.", winsock.StdErr);

        var ip = await context.Executor.ExecuteAsync(
            SystemCommandKey.NetShIntIpReset, ["int", "ip", "reset"], ct);
        if (!ip.Success)
            return OperationResult.Fail("Falló netsh int ip reset.", ip.StdErr);

        return OperationResult.Ok("Pila de red reparada. Reinicie para completar.");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Fail(
            "El reseteo de pila no es reversible; use un punto de restauración.",
            "not-reversible"));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default) =>
        Task.FromResult(VerificationResult.PendingReboot("Reparación aplicada; pendiente de reinicio para surtir efecto."));

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Solo con síntomas de red."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine { Kind = "Command", Target = "netsh winsock reset", Before = "catálogo actual", After = "catálogo restablecido" },
                new PreviewLine { Kind = "Command", Target = "netsh int ip reset", Before = "configuración actual", After = "configuración restablecida" },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
