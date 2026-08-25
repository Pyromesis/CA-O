using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Performance;

/// <summary>#2: activate the Ultimate/High performance power plan with real revert.</summary>
public sealed class MaximumPowerPlan : IOptimization
{
    public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string UltimatePerformanceGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Plan de energía: máximo rendimiento",
        NameEn = "Power plan: high performance",
        DescriptionEs = "Activa el plan de alto rendimiento (crea el oculto 'Rendimiento máximo' si falta).",
        DescriptionEn = "Activates the high performance plan (creates hidden Ultimate Performance if missing).",
        TooltipEs = "Captura tu plan actual y lo restaura con la reversión. En portátiles con batería aumentará el consumo.",
        Category = OptimizationCategory.Performance,
        Impact = ImpactLevel.High,
    };

    private static string Id => "maximum-power-plan";

    /// <summary>Active scheme GUID parsed by the engine from powercfg.</summary>
    public string? ActiveSchemeGuid { get; set; }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        string.Equals(ActiveSchemeGuid, HighPerformanceGuid, StringComparison.OrdinalIgnoreCase)
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"scheme={ActiveSchemeGuid ?? "381b4222-f694-41f0-9685-ff5bb260df2e"}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Process is null) return OperationResult.Fail("Runner no disponible.", "IProcessRunner null");

        var (code, output) = await context.Process.RunAsync("powercfg", $"/setactive {HighPerformanceGuid}", ct);
        if (code != 0)
        {
            // Hidden plan may not exist yet: duplicate it, then activate.
            var (_, duplicateOutput) = await context.Process.RunAsync(
                "powercfg", $"/duplicatescheme {UltimatePerformanceGuid}", ct);
            (code, output) = await context.Process.RunAsync("powercfg", $"/setactive {HighPerformanceGuid}", ct);
            if (code != 0) return OperationResult.Fail("No se pudo activar el plan de rendimiento.", output + duplicateOutput);
        }
        return OperationResult.Ok("Plan de máximo rendimiento activado.");
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Process is null) return OperationResult.Fail("Runner no disponible.", "IProcessRunner null");
        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("scheme=", StringComparison.Ordinal));
        var guid = note?["scheme=".Length..];
        if (string.IsNullOrWhiteSpace(guid)) return OperationResult.Ok("Sin plan previo registrado; nada que restaurar.");

        var (code, output) = await context.Process.RunAsync("powercfg", $"/setactive {guid}", ct);
        return code == 0
            ? OperationResult.Ok("Plan de energía anterior restaurado.")
            : OperationResult.Fail("No se pudo restaurar el plan anterior.", output);
    }
}
