using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Performance;

/// <summary>
/// Desactiva el tick dinámico (bcdedit disabledynamictick yes): el reloj del
/// planificador corre siempre en vez de pararse en idle, lo que estabiliza
/// la latencia en algunos equipos (DPC/frame pacing). Documentado por
/// Microsoft. Requiere reinicio. Revertir restaura el default (deletevalue).
/// </summary>
public sealed class DisableDynamicTick : IOptimization
{
    private int? _lastExitCode;

    public OptimizationDefinition Definition => new()
    {
        Id = "disable-dynamic-tick",
        NameEs = "Tick estable (sin tick dinámico)",
        NameEn = "Steady tick (no dynamic tick)",
        DescriptionEs = "El planificador no detiene su reloj en idle (bcdedit).",
        DescriptionEn = "The scheduler never stops its clock at idle (bcdedit).",
        TooltipEs = "Puede estabilizar latencia/DPC en algunos equipos; en otros no cambia nada y gasta algo más en idle. Requiere reinicio. Reversible al default.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.RequiresReboot,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add("dynamic-tick=revertir con deletevalue (default de Windows)");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.BcdEditDynamicTickYes,
            ["/set", "{current}", "disabledynamictick", "yes"], ct);
        _lastExitCode = result.ExitCode;

        return result.Success
            ? OperationResult.Ok("Tick dinámico desactivado. Reinicia para aplicar.")
            : OperationResult.Fail("bcdedit falló.", result.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.BcdEditDynamicTickDelete,
            ["/deletevalue", "{current}", "disabledynamictick"], ct);
        return result.Success
            ? OperationResult.Ok("Tick dinámico restaurado al default de Windows. Reinicia para aplicar.")
            : OperationResult.Fail("No se pudo restaurar el tick dinámico.", result.StdErr);
    }

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastExitCode is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }

        return Task.FromResult(_lastExitCode == 0
            ? VerificationResult.Passed(OptimizationState.Unknown, "bcdedit exit=0. Reinicia para que surta efecto.")
            : VerificationResult.Failed(OptimizationState.Unknown, $"bcdedit terminó con exit={_lastExitCode}."));
    }
}
