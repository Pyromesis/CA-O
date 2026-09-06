using System.Management;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.System;

/// <summary>Creates a real system restore point via WMI before optimization batches.</summary>
public sealed class CreateRestorePointBeforeOptimizationBatch : IOptimization
{
    private bool? _lastCreated;

    public OptimizationDefinition Definition => new()
    {
        Id = "create-restore-point-before-optimization-batch",
        NameEs = "Crear punto restauracion antes de lote",
        NameEn = "Create restore point before batch",
        DescriptionEs = "Crea un punto de restauración real del sistema antes de aplicar un lote.",
        DescriptionEn = "Creates a real system restore point before applying a batch.",
        TooltipEs = "Invoca SystemRestore.CreateRestorePoint por WMI. Windows limita la frecuencia.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.IncreasedProtection,
        Impact = ImpactLevel.Medium,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"restore-point-requested={DateTime.UtcNow:O}");
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        try
        {
            var result = Task.Run(() =>
            {
                var scope = new ManagementScope(@"root\default");
                scope.Connect();

                using var sysRestoreClass = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);
                var inParams = sysRestoreClass.GetMethodParameters("CreateRestorePoint");
                inParams["Description"] = "CA-O antes de lote de optimización";
                inParams["RestorePointType"] = 12;
                inParams["EventType"] = 100;

                var outParams = sysRestoreClass.InvokeMethod("CreateRestorePoint", inParams, null);
                return outParams?["ReturnValue"] is null || Convert.ToUInt32(outParams["ReturnValue"]) == 0;
            }, ct).GetAwaiter().GetResult();

            _lastCreated = result;
            return Task.FromResult(result
                ? OperationResult.Ok("Punto de restauración creado.")
                : OperationResult.Fail("El sistema devolvió un error al crear el punto.", "restore-point-failed"));
        }
        catch (Exception ex)
        {
            _lastCreated = false;
            return Task.FromResult(OperationResult.Fail(
                "No se pudo crear el punto de restauración: " + ex.Message.Trim(), "restore-point-error"));
        }
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("El punto de restauración se conserva como red de seguridad."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastCreated is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de creación en esta sesión."));
        }

        return Task.FromResult(_lastCreated == true
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Punto de restauración verificado creado.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "No se creó el punto de restauración."));
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Requiere protección del sistema activa en C:."));
}
