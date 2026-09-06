using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Power;

/// <summary>
/// Base para ajustes reales de plan de energía en AC vía powercfg:
/// escribe el índice AC del ajuste, lo verifica releyendo y revierte al previo.
/// </summary>
public abstract class PowerAcSettingOptimization : IOptimization
{
    public abstract OptimizationDefinition Definition { get; }

    protected abstract string SubGuid { get; }
    protected abstract string SettingGuid { get; }
    protected abstract string TargetIndex { get; }

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    private async Task<string?> QueryCurrentAsync(Core.Interfaces.IPrivilegedCommandExecutor executor, CancellationToken ct)
    {
        var query = await executor.ExecuteAsync(
            SystemCommandKey.PowerCfgQueryAcValueIndex,
            ["/q", "SCHEME_CURRENT", SubGuid, SettingGuid], ct);
        if (!query.Success) return null;
        var match = Regex.Match(query.StdOut,
            @"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.TrimStart('0') is var hex && hex.Length == 0 ? "0" : hex : null;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var previous = await QueryCurrentAsync(context.Executor, ct);
        var set = await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", "SCHEME_CURRENT", SubGuid, SettingGuid, TargetIndex], ct);
        if (!set.Success)
            return OperationResult.Fail("No se pudo aplicar el ajuste de energía.", set.StdErr);

        var activate = await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgSetActiveCurrent, ["/setactive", "SCHEME_CURRENT"], ct);
        if (!activate.Success)
            return OperationResult.Fail("No se pudo activar el plan tras el ajuste.", activate.StdErr);

        _lastPrevious = previous;
        return OperationResult.Ok($"Ajuste de energía aplicado (índice {TargetIndex}).");
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var previous = snapshot.RawNotes
            .FirstOrDefault(n => n.StartsWith("power-index=", StringComparison.Ordinal))?["power-index=".Length..]
            ?? _lastPrevious ?? "1";
        if (previous is not ("0" or "1" or "2")) previous = "1";

        var set = await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgSetAcValueIndex,
            ["/setacvalueindex", "SCHEME_CURRENT", SubGuid, SettingGuid, previous], ct);
        if (!set.Success)
            return OperationResult.Fail("No se pudo revertir el ajuste de energía.", set.StdErr);

        await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgSetActiveCurrent, ["/setactive", "SCHEME_CURRENT"], ct);
        return OperationResult.Ok($"Ajuste de energía restaurado a índice {previous}.");
    }

    public async Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return VerificationResult.Unknown(OptimizationState.Unknown, "Ejecutor no disponible.");

        var current = await QueryCurrentAsync(context.Executor, ct);
        if (current is null)
            return VerificationResult.Unknown(OptimizationState.Unknown, "No se pudo releer el ajuste.");

        return current == TargetIndex
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, $"Índice verificado: {current}.")
            : VerificationResult.Failed(OptimizationState.NotApplied, $"Índice actual {current}, esperado {TargetIndex}.");
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
                    Target = $"powercfg /setacvalueindex SCHEME_CURRENT {SubGuid} {SettingGuid}",
                    Before = "índice actual",
                    After = $"índice {TargetIndex}",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });

    private string? _lastPrevious;
}
