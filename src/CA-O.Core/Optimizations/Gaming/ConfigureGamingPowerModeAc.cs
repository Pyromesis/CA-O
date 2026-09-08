using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Gaming;

/// <summary>
/// Activates the hidden Ultimate Performance plan for gaming on AC power,
/// creating it first when missing. Captures the previous scheme for revert.
/// </summary>
public sealed class ConfigureGamingPowerModeAc : IOptimization
{
    public const string UltimatePerformanceGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public OptimizationDefinition Definition => new()
    {
        Id = "configure-gaming-power-mode-ac",
        NameEs = "Plan Rendimiento máximo en AC para juegos",
        NameEn = "Ultimate Performance plan on AC for gaming",
        DescriptionEs = "Activa el plan oculto Rendimiento máximo con powercfg en AC. Nunca se aplica en batería.",
        DescriptionEn = "Activates the hidden Ultimate Performance plan via powercfg on AC. Never on battery.",
        TooltipEs = "Duplica el esquema Ultimate si falta y lo activa. Guarda el previo para revertir.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    public OptimizationState Detect(IRegistryAccessor registry) =>
        Optimization.PowerSchemes.DetectScheme(registry, UltimatePerformanceGuid);

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add(Optimization.PowerSchemes.CaptureSchemeNote(registry));
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var current = await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgQueryActiveScheme, ["/getactivescheme"], ct);
        _lastPrevious = current.Success ? ParseScheme(current.StdOut) : null;

        await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgDuplicateScheme, ["/duplicatescheme", UltimatePerformanceGuid], ct);

        var set = await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgSetActiveScheme, ["/setactive", UltimatePerformanceGuid], ct);
        if (!set.Success)
            return OperationResult.Fail("No se pudo activar el plan Rendimiento máximo.", set.StdErr);

        return OperationResult.Ok("Plan Rendimiento máximo activado para gaming en AC.");
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("scheme=", StringComparison.Ordinal));
        var previous = note?["scheme=".Length..] ?? _lastPrevious;
        if (string.IsNullOrWhiteSpace(previous))
            return OperationResult.Fail("Sin plan previo registrado; nada que restaurar.", "no-previous-scheme");

        var restore = await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgSetActiveScheme, ["/setactive", previous], ct);
        return restore.Success
            ? OperationResult.Ok("Plan de energía anterior restaurado.")
            : OperationResult.Fail("No se pudo restaurar el plan anterior.", restore.StdErr);
    }

    public async Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return VerificationResult.Unknown(OptimizationState.Unknown, "Ejecutor no disponible.");

        var query = await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgQueryActiveScheme, ["/getactivescheme"], ct);
        if (!query.Success)
            return VerificationResult.Unknown(OptimizationState.Unknown, "No se pudo consultar el plan: " + query.StdErr);

        return query.StdOut.Contains(UltimatePerformanceGuid, StringComparison.OrdinalIgnoreCase)
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Plan Rendimiento máximo verificado activo.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "El plan activo no es Rendimiento máximo.");
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(context.OnBattery
            ? PreconditionResult.Fail("Solo con alimentación AC.")
            : PreconditionResult.Ok("Con alimentación AC."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "PowerCfg",
                    Target = $"powercfg /setactive {UltimatePerformanceGuid}",
                    Before = "plan actual",
                    After = "Rendimiento máximo",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });

    private static string? ParseScheme(string output)
    {
        var match = Regex.Match(output,
            @"Power Scheme GUID:\s*([0-9a-fA-F-]{36})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? _lastPrevious;
}
