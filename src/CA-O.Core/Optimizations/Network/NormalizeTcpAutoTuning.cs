using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Network;

/// <summary>#18: netsh autotuninglevel=normal — the MODERN recommended value,
/// captured and restored exactly on revert. Never set to disabled.</summary>
public sealed class NormalizeTcpAutoTuning : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Auto-ajuste TCP: normal",
        NameEn = "TCP autotuning: normal",
        DescriptionEs = "Garantiza que el auto-ajuste de ventana TCP esté en 'normal' (valor moderno correcto).",
        DescriptionEn = "Ensures TCP receive-window autotuning is 'normal' (the modern default).",
        TooltipEs = "Los tweaks antiguos ponían 'disabled' y EMPEORABAN el rendimiento hoy. Esta acción solo restaura el valor sano si algo lo cambió.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    private static string Id => "normalize-tcp-autotuning";

    /// <summary>Current level parsed by the engine from `netsh int tcp show global`.</summary>
    public string? CurrentLevel { get; set; } = "normal";

    public OptimizationState Detect(IRegistryAccessor registry) =>
        string.Equals(CurrentLevel?.Trim(), "normal", StringComparison.OrdinalIgnoreCase)
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"autotuning={CurrentLevel ?? "normal"}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
        {
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");
        }
        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.NetShTcpAutotuningNormal,
            ["int", "tcp", "set", "global", "autotuninglevel=normal"], ct);
        return result.Success
            ? OperationResult.Ok("Auto-ajuste TCP en 'normal'.")
            : OperationResult.Fail("No se pudo cambiar el auto-ajuste TCP.", result.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
        {
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");
        }
        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("autotuning=", StringComparison.Ordinal));
        var previous = note?["autotuning=".Length..]?.Trim();
        if (string.IsNullOrWhiteSpace(previous) || previous.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Ok("Ya estaba en normal antes; nada que restaurar.");
        }

        // The gateway only accepts the five documented levels, so an exotic
        // captured value cannot be smuggled through.
        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.NetShTcpAutotuningNormal,
            ["int", "tcp", "set", "global", $"autotuninglevel={previous}"], ct);
        return result.Success
            ? OperationResult.Ok($"Nivel anterior '{previous}' restaurado.")
            : OperationResult.Fail("No se pudo restaurar el nivel TCP.", result.StdErr);
    }
}
