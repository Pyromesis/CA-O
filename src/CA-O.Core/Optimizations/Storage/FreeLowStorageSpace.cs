using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Read-only audit: reports when the system drive is low on free space.</summary>
public sealed class FreeLowStorageSpace : IOptimization
{
    private const long LowThresholdBytes = 10L * 1024 * 1024 * 1024;

    public OptimizationDefinition Definition => new()
    {
        Id = "free-low-storage-space",
        NameEs = "Auditar espacio bajo",
        NameEn = "Audit low disk space",
        DescriptionEs = "Audita si la unidad del sistema tiene poco espacio libre. Solo informa, no borra nada.",
        DescriptionEn = "Audits whether the system drive is low on free space. Reports only, deletes nothing.",
        TooltipEs = "Solo diagnóstico: no modifica ni borra archivos.",
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

    private static long FreeBytes()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch { return long.MaxValue; }
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        FreeBytes() < LowThresholdBytes ? OptimizationState.NotApplied : OptimizationState.AppliedByCao;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("Auditoría de espacio completada. Sin cambios: use las limpiezas para liberar."));

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La auditoría no tiene cambios que revertir."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var observed = Detect(context.Registry);
        return Task.FromResult(observed switch
        {
            OptimizationState.AppliedByCao =>
                VerificationResult.Passed(observed, "Espacio libre suficiente verificado."),
            _ => VerificationResult.Failed(observed, "La unidad del sistema sigue con poco espacio libre."),
        });
    }
}
