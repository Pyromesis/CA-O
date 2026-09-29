using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Configuración unificada de Storage Sense: escribe los 6 valores de
/// StoragePolicies de una vez (activación + cadencia diaria + temporales
/// 30 días + papelera 30 días). Sustituye a las 3 tarjetas anteriores
/// (enable-storage-sense, storage-sense-temp-cleanup y
/// storage-sense-recycle-bin-policy), que aplicaban el mismo valor por
/// separado y obligaban a tres pasadas para el mismo efecto.
/// </summary>
public sealed class ConfigureStorageSense : RegistryOptimizationBase
{
    internal const string PoliciesKey =
        @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicies";

    protected override IReadOnlyList<ValueTarget> Targets { get; } =
        new[]
        {
            // 01: Storage Sense activado. 02: cadencia diaria.
            new ValueTarget(RegistryHive2.CurrentUser, PoliciesKey, "01", 1, RegistryValueKind2.DWord),
            new ValueTarget(RegistryHive2.CurrentUser, PoliciesKey, "02", 0, RegistryValueKind2.DWord),
            // 03: limpieza de temporales activada. 06: retención 30 días.
            new ValueTarget(RegistryHive2.CurrentUser, PoliciesKey, "03", 1, RegistryValueKind2.DWord),
            new ValueTarget(RegistryHive2.CurrentUser, PoliciesKey, "06", 30, RegistryValueKind2.DWord),
            // 04: limpieza de papelera activada. 05: retención 30 días.
            new ValueTarget(RegistryHive2.CurrentUser, PoliciesKey, "04", 1, RegistryValueKind2.DWord),
            new ValueTarget(RegistryHive2.CurrentUser, PoliciesKey, "05", 30, RegistryValueKind2.DWord),
        };

    public override OptimizationDefinition Definition => new()
    {
        Id = "configure-storage-sense",
        NameEs = "Configurar Storage Sense",
        NameEn = "Configure Storage Sense",
        DescriptionEs = "Activa Storage Sense con limpieza diaria, temporales de 30 días y papelera de 30 días, todo de una vez.",
        DescriptionEn = "Enables Storage Sense with daily cadence, 30-day temp files and 30-day recycle bin, all at once.",
        TooltipEs = "Escribe los 6 valores de HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\StorageSense\\Parameters\\StoragePolicies en una sola pasada. Reversible via snapshot.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok("Storage Sense configurado (diario + temporales 30 días + papelera 30 días)."));
    }
}
