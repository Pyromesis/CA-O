using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// GlobalTimerResolutionRequests=1: desde Windows 10 2004 una petición de
/// timer resolution solo afecta al proceso que la hace, y en Windows 11 se
/// ignora si ese proceso no tiene ventana visible. Sin esta clave, el timer
/// que fija el servicio de CA-O (o ISLC, TimerResolution, etc.) no llega a
/// los juegos. Con ella vuelve el comportamiento global previo. Solo existe
/// en Windows 11 / Server 2022 (build 20348+). Requiere reinicio. Reversible.
/// </summary>
public sealed class EnableGlobalTimerResolution : RegistryOptimizationBase
{
    internal const string KernelKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel";
    internal const string ValueName = "GlobalTimerResolutionRequests";
    internal const int MinimumBuild = 20348;

    protected override IReadOnlyList<ValueTarget> Targets { get; } =
    [
        new(RegistryHive2.LocalMachine, KernelKey, ValueName, 1, RegistryValueKind2.DWord),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "enable-global-timer-resolution",
        NameEs = "Timer resolution global (Windows 11)",
        NameEn = "Global timer resolution (Windows 11)",
        DescriptionEs = "Hace que el timer de alta resolución que fija CA-O llegue también a los juegos.",
        DescriptionEn = "Makes the high-resolution timer CA-O sets reach games too.",
        TooltipEs = "Sin esto, Windows 11 aplica el timer solo al proceso que lo pide y lo ignora si no tiene ventana visible. Activa GlobalTimerResolutionRequests. Requiere reinicio. Reversible.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.RequiresReboot,
    };

    public override Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(context.WindowsBuild >= MinimumBuild
            ? PreconditionResult.Ok("Windows 11 / Server 2022: la clave está soportada.")
            : PreconditionResult.Fail("Requiere Windows 11 (build 20348 o superior); en versiones anteriores el timer ya es global."));

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Timer resolution global activado. Reinicia para aplicar."));
    }
}
