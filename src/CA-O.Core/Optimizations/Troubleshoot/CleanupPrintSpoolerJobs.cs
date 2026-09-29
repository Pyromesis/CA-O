using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>
/// Limpia trabajos de impresión atascados (*.SPL/*.SHD en
/// %SystemRoot%\System32\spool\PRINTERS) con el Spooler detenido, y lo
/// rearranca después. Complemento de restart-print-spooler: reiniciar no
/// basta cuando el fichero del trabajo está corrupto. Pide confirmación en
/// UI (los trabajos en cola se pierden). Mantenimiento no reversible.
/// </summary>
public sealed class CleanupPrintSpoolerJobs : IOptimization
{
    internal const string SpoolerService = "Spooler";
    internal const string PrintersSubdir = @"System32\spool\PRINTERS";

    private int? _lastDeleted;
    private long _lastBytes;

    public OptimizationDefinition Definition => new()
    {
        Id = "cleanup-print-spooler-jobs",
        NameEs = "Limpiar trabajos de impresión atascados",
        NameEn = "Clean stuck print jobs",
        DescriptionEs = "Borra la cola de impresión detenida (*.SPL/*.SHD) y rearranca el Spooler.",
        DescriptionEn = "Clears the stopped print queue (*.SPL/*.SHD) and restarts the Spooler.",
        TooltipEs = "Detiene Spooler, borra trabajos atascados y lo rearranca. Los documentos en cola se pierden: pide confirmación. Complemento de restart-print-spooler. Mantenimiento no reversible.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    internal static string PrintersDir()
    {
        string root;
        try { root = Environment.GetFolderPath(Environment.SpecialFolder.Windows); }
        catch { root = @"C:\Windows"; }
        if (string.IsNullOrWhiteSpace(root))
            root = @"C:\Windows";
        return Path.Combine(root, PrintersSubdir);
    }

    internal static IReadOnlyList<FileInfo> PendingJobs()
    {
        var found = new List<FileInfo>();
        string dir;
        try { dir = PrintersDir(); }
        catch { return found; }
        DirectoryInfo info;
        try { info = new DirectoryInfo(dir); }
        catch { return found; }
        if (!info.Exists)
            return found;
        foreach (var pattern in new[] { "*.SPL", "*.SHD" })
        {
            FileInfo[] candidates;
            try { candidates = info.GetFiles(pattern, SearchOption.TopDirectoryOnly); }
            catch { continue; }
            found.AddRange(candidates);
        }
        return found;
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        PendingJobs().Count == 0 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add("spooler-jobs=mantenimiento puntual (sin estado previo que guardar)");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null)
            return OperationResult.Fail("Gestor de servicios no disponible.", "no-services");

        var pending = PendingJobs();
        if (pending.Count == 0)
            return OperationResult.Ok("Cola de impresión vacía: nada que limpiar.");

        if (string.Equals(context.Services.GetStartType(SpoolerService), "Disabled", StringComparison.OrdinalIgnoreCase))
            return OperationResult.Fail("Cola de impresión: el servicio está deshabilitado a propósito y no se toca.", "service-disabled");
        if (!context.Services.Exists(SpoolerService))
            return OperationResult.Fail("Cola de impresión: servicio no encontrado en este equipo.", "service-not-found");

        // Solo se rearranca si estaba en marcha: si estaba detenido a
        // propósito, se deja detenido tras limpiar.
        var wasRunning = true;
        try { await context.Services.StopAsync(SpoolerService, ct); }
        catch { wasRunning = false; }

        int files = 0;
        long bytes = 0;
        foreach (var file in pending)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                bytes += file.Length;
                file.Delete();
                files++;
            }
            catch { /* en uso: se omite */ }
        }

        if (wasRunning)
        {
            try { await context.Services.StartAsync(SpoolerService, ct); }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Trabajos borrados ({files}), pero no se pudo rearrancar el Spooler: {ex.Message}", "spooler-not-restarted");
            }
        }

        _lastDeleted = files;
        _lastBytes = bytes;
        return files > 0
            ? OperationResult.Ok($"Cola de impresión limpia: {files} trabajo(s), {bytes / 1024} KB. Spooler rearrancado.")
            : OperationResult.Fail("No se pudo borrar ningún trabajo (en uso o sin acceso).", "apply-failed");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La limpieza de la cola no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastDeleted is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }
        return Task.FromResult(PendingJobs().Count == 0
            ? VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} trabajo(s) eliminados, cola vacía.")
            : VerificationResult.Failed(OptimizationState.NotApplied,
                "La cola sigue con trabajos pendientes tras limpiar."));
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var pending = PendingJobs();
        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Cleanup",
                    Target = Path.Combine("%SystemRoot%", PrintersSubdir) + "\\*.SPL,*.SHD",
                    Before = $"{pending.Count} trabajo(s) en cola",
                    After = "eliminados + Spooler rearrancado",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
    }
}
