using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.System;

/// <summary>Read-only audit: reports pending-reboot signals (WU, CBS, file renames).</summary>
public sealed class PendingRebootMaintenance : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "pending-reboot-maintenance",
        NameEs = "Mantenimiento reinicio pendiente",
        NameEn = "Pending reboot maintenance",
        Category = OptimizationCategory.Storage,
        DescriptionEs = "Audita señales de reinicio pendiente. Solo informa, no reinicia ni modifica nada.",
        DescriptionEn = "Audits pending-reboot signals. Reports only, reboots or changes nothing.",
        TooltipEs = "Solo diagnóstico: lee Windows Update, CBS y renombres pendientes.",
        ExpectedImpact = PerformanceImpact.DiagnosticOnly,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    private static bool HasPendingReboot(IRegistryAccessor registry)
    {
        if (registry.GetValueNames(
                RegistryHive2.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired").Count > 0)
            return true;
        if (registry.GetValueNames(
                RegistryHive2.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending").Count > 0)
            return true;
        var rename = registry.GetValue(
            RegistryHive2.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Session Manager", "PendingFileRenameOperations");
        return rename is string[] arr && arr.Any(s => !string.IsNullOrWhiteSpace(s));
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        HasPendingReboot(registry) ? OptimizationState.NotApplied : OptimizationState.AppliedByCao;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("Auditoría de reinicio completada. Sin cambios: reinicie manualmente si hay señales."));

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La auditoría no tiene cambios que revertir."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var observed = Detect(context.Registry);
        return Task.FromResult(observed switch
        {
            OptimizationState.AppliedByCao =>
                VerificationResult.Passed(observed, "Sin señales de reinicio pendiente."),
            _ => VerificationResult.Failed(observed, "Hay señales de reinicio pendiente."),
        });
    }
}
