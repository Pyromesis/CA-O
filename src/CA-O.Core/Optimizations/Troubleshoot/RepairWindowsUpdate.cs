using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Repairs Windows Update by renaming its data folders with services stopped.</summary>
public sealed class RepairWindowsUpdate : IOptimization
{
    private static string WindowsRoot
    {
        get
        {
            try
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                return string.IsNullOrWhiteSpace(root) ? @"C:\Windows" : root;
            }
            catch { return @"C:\Windows"; }
        }
    }

    private static readonly string[] ManagedServices = ["wuauserv", "bits", "cryptsvc", "msiserver"];

    public OptimizationDefinition Definition => new()
    {
        Id = "repair-windows-update",
        NameEs = "Reparar Windows Update",
        NameEn = "Repair Windows Update",
        DescriptionEs = "Renombra las carpetas de datos de Windows Update con los servicios detenidos. Corrige actualizaciones atascadas.",
        DescriptionEn = "Renames Windows Update data folders with services stopped. Fixes stuck updates.",
        TooltipEs = "Renombra SoftwareDistribution y Catroot2 a .bak y reanuda servicios. Reversible.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
    };

    private static IReadOnlyList<(string Original, string Backup)> RenameTargets() =>
    [
        (Path.Combine(WindowsRoot, "SoftwareDistribution"), Path.Combine(WindowsRoot, "SoftwareDistribution.bak")),
        (Path.Combine(WindowsRoot, "System32", "catroot2"), Path.Combine(WindowsRoot, "System32", "catroot2.bak")),
    ];

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        foreach (var (original, backup) in RenameTargets())
        {
            snapshot.RawNotes.Add($"dir-exists={original}:{Directory.Exists(original)}");
            snapshot.RawNotes.Add($"bak-exists={backup}:{Directory.Exists(backup)}");
        }
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is not null)
        {
            foreach (var name in ManagedServices)
            {
                try { await context.Services.StopAsync(name, ct); } catch { }
            }
        }

        var renamed = new List<string>();
        try
        {
            foreach (var (original, backup) in RenameTargets())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!Directory.Exists(original)) continue;
                    if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
                    Directory.Move(original, backup);
                    renamed.Add(original);
                }
                catch { }
            }
        }
        finally
        {
            if (context.Services is not null)
            {
                foreach (var name in ManagedServices)
                {
                    try { await context.Services.StartAsync(name, ct); } catch { }
                }
            }
        }

        _lastRenamed = renamed;
        return renamed.Count > 0
            ? OperationResult.Ok($"Windows Update reparado: {renamed.Count} carpeta(s) renovadas.")
            : OperationResult.Fail("No se pudo renovar ninguna carpeta.", "apply-failed");
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Services is not null)
        {
            foreach (var name in ManagedServices)
            {
                try { await context.Services.StopAsync(name, ct); } catch { }
            }
        }

        try
        {
            foreach (var (original, backup) in RenameTargets())
            {
                try
                {
                    if (!Directory.Exists(backup)) continue;
                    if (Directory.Exists(original)) Directory.Delete(original, recursive: true);
                    Directory.Move(backup, original);
                }
                catch { }
            }
            return OperationResult.Ok("Carpetas de Windows Update restauradas.");
        }
        finally
        {
            if (context.Services is not null)
            {
                foreach (var name in ManagedServices)
                {
                    try { await context.Services.StartAsync(name, ct); } catch { }
                }
            }
        }
    }

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastRenamed is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }

        var fresh = RenameTargets().All(t => Directory.Exists(t.Original));
        return Task.FromResult(fresh
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Carpetas renovadas verificadas.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "Faltan carpetas renovadas."));
    }

    private IReadOnlyList<string>? _lastRenamed;
}
