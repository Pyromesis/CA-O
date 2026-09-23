using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Core.Rollback;
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

    /// <summary>
    /// El índice AC solo es observable vía powercfg (requiere ejecutor, no
    /// registro): Detect honesto es Unknown. La regla OneShot del motor de
    /// recomendaciones evita el nagging tras aplicar (ver OneShotLedger).
    /// </summary>
    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

    /// <summary>Las escrituras powercfg se serializan por el plan activo.</summary>
    public virtual IReadOnlyList<ResourceKey> ResourceKeys => [ResourceKey.PowerPlan()];

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot { TimestampUtc = DateTime.UtcNow };
        snapshot.RawNotes.Add($"power-target={TargetIndex}");
        snapshot.RawNotes.Add($"power-sub={SubGuid}");
        snapshot.RawNotes.Add($"power-setting={SettingGuid}");
        return snapshot;
    }

    private async Task<string?> QueryCurrentAsync(Core.Interfaces.IPrivilegedCommandExecutor executor, CancellationToken ct)
    {
        var query = await executor.ExecuteAsync(
            SystemCommandKey.PowerCfgQueryAcValueIndex,
            ["/q", "SCHEME_CURRENT", SubGuid, SettingGuid], ct);
        if (!query.Success) return null;
        var match = Regex.Match(query.StdOut,
            @"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        // Normaliza a minúsculas sin ceros a la izquierda: powercfg emite
        // hex (p. ej. 0x0000000a) y TargetIndex puede ser decimal.
        var hex = match.Groups[1].Value.TrimStart('0').ToLowerInvariant();
        return hex.Length == 0 ? "0" : hex;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var previous = await QueryCurrentAsync(context.Executor, ct);
        if (previous is null)
        {
            // El ajuste no se puede leer (sin hardware compatible o esquema
            // sin ese subgrupo): fallar ANTES de mutar, no "éxito" seguido
            // de Unknown + rollback de un cambio fantasma.
            return OperationResult.Fail(
                "Esta máquina no expone ese ajuste de energía (hardware no compatible).",
                "not-supported");
        }
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
            ?? _lastPrevious;
        if (previous is null)
        {
            // Sin índice previo registrado (snapshot antiguo o Apply en otro
            // proceso): fallar ANTES de mutar en vez de inventar un índice.
            return OperationResult.Fail(
                "No hay índice previo registrado para este ajuste; no se puede revertir sin adivinar.",
                "no-previous-index");
        }
        if (previous is not ("0" or "1" or "2")) previous = NormalizeIndex(previous);

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

        return NormalizeIndex(current) == NormalizeIndex(TargetIndex)
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
                    Before = "índice actual (se lee con powercfg al aplicar)",
                    After = $"índice {TargetIndex}",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });

    private static string NormalizeIndex(string index)
    {
        var n = index.Trim().TrimStart('0').ToLowerInvariant();
        return n.Length == 0 ? "0" : n;
    }

    private string? _lastPrevious;
}
