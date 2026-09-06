using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Cleans Windows Update download leftovers (SoftwareDistribution\Download).</summary>
public sealed class DiskCleanupSystemFiles : IOptimization
{
    private static string DownloadDir
    {
        get
        {
            string root;
            try { root = Environment.GetFolderPath(Environment.SpecialFolder.Windows); }
            catch { root = @"C:\Windows"; }
            if (string.IsNullOrWhiteSpace(root)) root = @"C:\Windows";
            return Path.Combine(root, "SoftwareDistribution", "Download");
        }
    }

    public OptimizationDefinition Definition => new()
    {
        Id = "disk-cleanup-system-files",
        NameEs = "Limpieza de archivos del sistema",
        NameEn = "Disk cleanup system files",
        DescriptionEs = "Borra restos de descargas de Windows Update. Detiene y reanuda los servicios implicados.",
        DescriptionEn = "Deletes Windows Update download leftovers. Stops and resumes the services involved.",
        TooltipEs = "Vacía SoftwareDistribution\\Download con wuauserv/bits detenidos. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
        Flags = OptimizationFlags.NotReversible,
    };

    private static IReadOnlyList<string> PendingFiles()
    {
        var found = new List<string>();
        DirectoryInfo info;
        try { info = new DirectoryInfo(DownloadDir); }
        catch { return found; }
        if (!info.Exists) return found;
        FileInfo[] files;
        try { files = info.GetFiles("*.*", SearchOption.AllDirectories); }
        catch { return found; }
        foreach (var file in files)
        {
            try { found.Add(file.FullName); }
            catch { }
        }
        return found;
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        PendingFiles().Count == 0 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"download-files={PendingFiles().Count}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var pending = PendingFiles();
        if (pending.Count == 0)
            return await Task.FromResult(OperationResult.Ok("No quedaban restos de Windows Update para limpiar."));

        if (context.Services is not null)
        {
            try { await context.Services.StopAsync("wuauserv", ct); } catch { }
            try { await context.Services.StopAsync("bits", ct); } catch { }
        }

        var deleted = 0;
        long bytes = 0L;
        try
        {
            foreach (var path in pending)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var length = new FileInfo(path).Length;
                    File.Delete(path);
                    deleted++;
                    bytes += length;
                }
                catch { }
            }
        }
        finally
        {
            if (context.Services is not null)
            {
                try { await context.Services.StartAsync("wuauserv", ct); } catch { }
                try { await context.Services.StartAsync("bits", ct); } catch { }
            }
        }

        _lastDeleted = deleted;
        return OperationResult.Ok($"Limpieza completada: {deleted} fichero(s), {bytes / 1024} KB liberados.");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La limpieza de archivos del sistema no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastDeleted is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }

        return Task.FromResult(PendingFiles().Count == 0
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Descargas de Windows Update verificadas limpias.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "Aún quedan restos (posiblemente en uso)."));
    }

    private int? _lastDeleted;

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Sin precondiciones específicas."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Cleanup",
                    Target = DownloadDir,
                    Before = $"{PendingFiles().Count} fichero(s)",
                    After = "eliminados",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
