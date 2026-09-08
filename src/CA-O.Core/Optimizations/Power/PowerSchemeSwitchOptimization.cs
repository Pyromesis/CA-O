using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Power;

/// <summary>
/// Base para activar un plan de energía real (powercfg /setactive) capturando
/// el esquema previo para restaurarlo exacto al revertir.
/// </summary>
public abstract class PowerSchemeSwitchOptimization : IOptimization
{
    public abstract OptimizationDefinition Definition { get; }

    protected abstract string TargetScheme { get; }
    protected abstract string TargetLabel { get; }

    private static string? ParseActiveScheme(string output)
    {
        var match = Regex.Match(output,
            @"Power Scheme GUID:\s*([0-9a-fA-F-]{36})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        Optimization.PowerSchemes.DetectScheme(registry,
            Optimization.PowerSchemes.ResolveSchemeGuid(TargetScheme));

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
        _lastPrevious = current.Success ? ParseActiveScheme(current.StdOut) : null;

        var set = await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgSetActiveScheme, ["/setactive", TargetScheme], ct);
        if (!set.Success)
            return OperationResult.Fail($"No se pudo activar el plan {TargetLabel}.", set.StdErr);

        return OperationResult.Ok($"Plan {TargetLabel} activado.");
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

        return query.StdOut.Contains(TargetScheme, StringComparison.OrdinalIgnoreCase)
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, $"Plan {TargetLabel} verificado activo.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "El plan activo no coincide tras aplicar.");
    }

    public virtual Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Sin precondiciones específicas."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "PowerCfg",
                    Target = $"powercfg /setactive {TargetScheme}",
                    Before = "plan actual",
                    After = TargetLabel,
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });

    private string? _lastPrevious;
}
