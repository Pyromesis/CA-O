using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>#9: powercfg /h off — frees hiberfil.sys GBs.</summary>
public sealed class DisableHibernate : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar hibernación",
        NameEn = "Disable hibernation",
        DescriptionEs = "Elimina hiberfil.sys y libera varios GB en disco.",
        DescriptionEn = "Removes hiberfil.sys and frees several GB of disk space.",
        TooltipEs = "Pierdes la hibernación (Inicio rápido usa parte de ella). El archivo se elimina al desactivar.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.None,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.NoKnownConflict,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    private static string Id => "disable-hibernate";

    private const string PowerKeyPath = @"SYSTEM\CurrentControlSet\Control\Power";
    private const string HibernateValue = "HibernateEnabled";

    /// <summary>
    /// Hibernation availability injected from `powercfg /a` (UI process).
    /// Solo fallback: en el servicio este valor nunca se inyecta y el estado
    /// se lee SIEMPRE en vivo del registro (antes fallaba la verificación
    /// con CAO-TXN-003 y la reversión reactivaba la hibernación).
    /// </summary>
    public bool HibernateAvailable { get; set; } = true;

    /// <summary>
    /// Lee el estado vivo: HibernateEnabled=0 => desactivada.
    /// null = valor ausente o ilegible (se usa el fallback inyectado).
    /// </summary>
    private static bool? ReadHibernateOff(IRegistryAccessor registry)
    {
        try
        {
            var raw = registry.GetValue(RegistryHive2.LocalMachine, PowerKeyPath, HibernateValue);
            if (raw is null) return null;
            var num = raw switch
            {
                int i => (long)i,
                uint u => u,
                long l => l,
                string s when long.TryParse(s, out var parsed) => parsed,
                _ => (long?)null,
            };
            if (num is null) return null;
            return num == 0;
        }
        catch
        {
            return null;
        }
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var off = ReadHibernateOff(registry);
        if (off.HasValue)
            return off.Value ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;
        return HibernateAvailable ? OptimizationState.NotApplied : OptimizationState.AppliedByCao;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        var off = ReadHibernateOff(registry);
        snapshot.RawNotes.Add($"hibernate={(off == true ? "off" : "available")}");
        return snapshot;
    }

    public async Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        // Estado vivo con un reintento: powercfg escribe el valor de forma
        // síncrona, pero no se declara un fallo por un hipo de lectura.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var off = ReadHibernateOff(context.Registry);
            if (off == true)
                return VerificationResult.Passed(OptimizationState.AppliedByCao, "Hibernación desactivada (HibernateEnabled=0).");
            if (off == false)
                return VerificationResult.Failed(OptimizationState.NotApplied, "La hibernación sigue activada tras aplicar.");
            try { await Task.Delay(500, ct); } catch { }
        }
        return VerificationResult.Unknown(OptimizationState.Unknown, "No se pudo leer el estado de hibernación.");
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
        {
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");
        }
        var result = await context.Executor.ExecuteAsync(SystemCommandKey.PowerCfgHibernateOff, ["/h", "off"], ct);
        return result.Success
            ? OperationResult.Ok("Hibernación desactivada; hiberfil.sys liberado.")
            : OperationResult.Fail("No se pudo desactivar la hibernación.", result.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
        {
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");
        }
        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("hibernate=", StringComparison.Ordinal));
        if (note == "hibernate=off") return OperationResult.Ok("Ya estaba desactivada antes; nada que restaurar.");

        var result = await context.Executor.ExecuteAsync(SystemCommandKey.PowerCfgHibernateOn, ["/h", "on"], ct);
        return result.Success
            ? OperationResult.Ok("Hibernación reactivada.")
            : OperationResult.Fail("No se pudo reactivar la hibernación.", result.StdErr);
    }
}
