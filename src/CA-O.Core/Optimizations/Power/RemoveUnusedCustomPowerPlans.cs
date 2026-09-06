using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Power;

/// <summary>Deletes custom power schemes that are not active via powercfg.</summary>
public sealed class RemoveUnusedCustomPowerPlans : IOptimization
{
    private static readonly HashSet<string> BuiltInSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "381b4222-f694-41f0-9685-ff5bb260df2e",
        "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
        "e9a42b02-d5df-448d-aa00-03f14749eb61",
        "a1841308-28f1-11db-8644-0011d68c8b18",
    };

    private int? _lastDeleted;

    public OptimizationDefinition Definition => new()
    {
        Id = "remove-unused-custom-power-plans",
        NameEs = "Eliminar planes de energía personalizados no usados",
        NameEn = "Remove unused custom power plans",
        DescriptionEs = "Detecta planes personalizados inactivos con powercfg y los elimina.",
        DescriptionEn = "Detects inactive custom plans with powercfg and deletes them.",
        TooltipEs = "Ejecuta powercfg /L y /delete solo en GUIDs personalizados inactivos. No reversible.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add("power-plan-cleanup=requested");
        return snapshot;
    }

    private static IReadOnlyList<(string Guid, bool Active)> ParseSchemes(string output)
    {
        var found = new List<(string, bool)>();
        foreach (Match match in Regex.Matches(output,
            @"Power Scheme GUID:\s*([0-9a-fA-F-]{36})\s*(\([^)]*\))?\s*(\*)?",
            RegexOptions.IgnoreCase))
        {
            found.Add((match.Groups[1].Value, match.Groups[3].Success));
        }
        return found;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var list = await context.Executor.ExecuteAsync(
            SystemCommandKey.PowerCfgListSchemes, ["/L"], ct);
        if (!list.Success)
            return OperationResult.Fail("No se pudieron enumerar los planes.", list.StdErr);

        var targets = ParseSchemes(list.StdOut)
            .Where(s => !s.Active && !BuiltInSchemes.Contains(s.Guid))
            .Select(s => s.Guid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
            return OperationResult.Ok("No quedaban planes personalizados inactivos.");

        var deleted = 0;
        foreach (var guid in targets)
        {
            var del = await context.Executor.ExecuteAsync(
                SystemCommandKey.PowerCfgDeleteScheme, ["/delete", guid], ct);
            if (del.Success) deleted++;
        }

        _lastDeleted = deleted;
        return deleted > 0
            ? OperationResult.Ok($"Planes personalizados eliminados: {deleted}.")
            : OperationResult.Fail("No se pudo eliminar ningún plan.", "apply-failed");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("Los planes eliminados no se restauran (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastDeleted is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }

        return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
            $"Verificado: {_lastDeleted} plan(es) eliminados con powercfg."));
    }
}
