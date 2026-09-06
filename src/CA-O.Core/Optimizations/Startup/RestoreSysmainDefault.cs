using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

/// <summary>Restores the SysMain (Superfetch) service to automatic start.</summary>
public sealed class RestoreSysmainDefault : IOptimization
{
    private const string ServiceName = "SysMain";
    private const string KeyPath = @"SYSTEM\CurrentControlSet\Services\SysMain";

    public OptimizationDefinition Definition => new()
    {
        Id = "restore-sysmain-default",
        NameEs = "Restaurar SysMain por defecto",
        NameEn = "Restore SysMain default",
        DescriptionEs = "Devuelve el servicio SysMain a inicio automático si un tweak externo lo deshabilitó.",
        DescriptionEn = "Returns the SysMain service to automatic start if an external tweak disabled it.",
        TooltipEs = "Establece HKLM Start=2 en el servicio SysMain. Reversible exacto.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var start = registry.GetValue(RegistryHive2.LocalMachine, KeyPath, "Start");
        return Normalize(start) == 2 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        var raw = registry.GetValueRaw(RegistryHive2.LocalMachine, KeyPath, "Start", out var kind);
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            RegistryHive2.LocalMachine.ToString(), KeyPath, "Start", raw,
            Existed: raw is not null)
        { Kind = raw is null ? RegistryValueKind2.None : kind });
        var delayed = registry.GetValueRaw(RegistryHive2.LocalMachine, KeyPath, "DelayedAutostart", out var delayedKind);
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            RegistryHive2.LocalMachine.ToString(), KeyPath, "DelayedAutostart", delayed,
            Existed: delayed is not null)
        { Kind = delayed is null ? RegistryValueKind2.None : delayedKind });
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null)
            return Task.FromResult(OperationResult.Fail("Gestor de servicios no disponible.", "no-services"));
        try
        {
            context.Services.SetStartType(ServiceName, "Automatic");
            return Task.FromResult(OperationResult.Ok("SysMain devuelto a inicio automático."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail("No se pudo restaurar SysMain.", ex.Message));
        }
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        foreach (var entry in snapshot.Registry)
        {
            var hive = Enum.Parse<RegistryHive2>(entry.Hive);
            if (entry.Existed && entry.Value is not null)
            {
                context.Registry.SetValueRaw(hive, entry.KeyPath, entry.ValueName, entry.Value, entry.Kind);
            }
            else
            {
                context.Registry.DeleteValue(hive, entry.KeyPath, entry.ValueName);
            }
        }
        return Task.FromResult(OperationResult.Ok("SysMain restaurado desde el snapshot."));
    }

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var observed = Detect(context.Registry);
        return Task.FromResult(observed switch
        {
            OptimizationState.AppliedByCao =>
                VerificationResult.Passed(observed, "SysMain verificado en automático."),
            _ => VerificationResult.Failed(observed, "SysMain no quedó en automático tras aplicar."),
        });
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Sin precondiciones específicas."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var before = registry.GetValue(RegistryHive2.LocalMachine, KeyPath, "Start")?.ToString() ?? "(ausente)";
        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Service",
                    Target = $@"HKLM\{KeyPath}\Start (SysMain)",
                    Before = before,
                    After = "2 (automático)",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
    }

    private static long? Normalize(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        string s => long.TryParse(s.Trim(), out var parsed) ? parsed : null,
        _ => null,
    };
}
