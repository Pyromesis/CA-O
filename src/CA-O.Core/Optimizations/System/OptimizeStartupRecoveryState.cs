using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.System;

/// <summary>Read-only audit: checks automatic restart on system failure is enabled.</summary>
public sealed class OptimizeStartupRecoveryState : IOptimization
{
    private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\CrashControl";

    public OptimizationDefinition Definition => new()
    {
        Id = "optimize-startup-recovery-state",
        NameEs = "Auditar recuperacion de inicio",
        NameEn = "Audit startup recovery",
        DescriptionEs = "Audita el reinicio automático ante fallo del sistema. Solo informa, no modifica nada.",
        DescriptionEn = "Audits automatic restart on system failure. Reports only, changes nothing.",
        TooltipEs = "Solo diagnóstico: lee CrashControl AutoReboot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.DiagnosticOnly,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var autoReboot = registry.GetValue(RegistryHive2.LocalMachine, KeyPath, "AutoReboot");
        return Normalize(autoReboot) == 1 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("Auditoría de recuperación completada. Sin cambios."));

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La auditoría no tiene cambios que revertir."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var observed = Detect(context.Registry);
        return Task.FromResult(observed switch
        {
            OptimizationState.AppliedByCao =>
                VerificationResult.Passed(observed, "Reinicio automático ante fallo verificado activo."),
            _ => VerificationResult.Failed(observed, "Reinicio automático ante fallo desactivado."),
        });
    }

    private static long? Normalize(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        string s => long.TryParse(s.Trim(), out var parsed) ? parsed : null,
        _ => null,
    };
}
