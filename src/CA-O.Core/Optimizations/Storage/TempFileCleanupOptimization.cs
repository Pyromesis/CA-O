using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Base para limpiezas reales de ficheros del sistema: borra ficheros (nunca
/// carpetas) anteriores al umbral en los directorios declarados. No reversible
/// por diseño (mantenimiento): el snapshot solo deja constancia.
/// </summary>
public abstract class TempFileCleanupOptimization : IOptimization
{
    public abstract OptimizationDefinition Definition { get; }

    /// <summary>Directorios, patrón y antigüedad mínima a limpiar.</summary>
    protected abstract IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; }

    private static string SystemRoot
    {
        get
        {
            try { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); }
            catch { return @"C:\Windows"; }
        }
    }

    protected static string ExpandDir(string dir) =>
        Environment.ExpandEnvironmentVariables(dir)
            .Replace("%SystemRoot%", SystemRoot, StringComparison.OrdinalIgnoreCase)
            .Replace("%WinDir%", SystemRoot, StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<string> PendingFiles()
    {
        var found = new List<string>();
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (directory, pattern, olderThanDays) in Targets)
        {
            string dir;
            try { dir = ExpandDir(directory); }
            catch { continue; }
            if (!seenDirs.Add(dir)) continue;
            DirectoryInfo info;
            try { info = new DirectoryInfo(dir); }
            catch { continue; }
            if (!info.Exists) continue;
            var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
            FileInfo[] files;
            try { files = info.GetFiles(pattern, SearchOption.TopDirectoryOnly); }
            catch { continue; }
            foreach (var file in files)
            {
                DateTime stamp;
                try { stamp = file.LastWriteTimeUtc; }
                catch { continue; }
                if (stamp <= cutoff) found.Add(file.FullName);
            }
        }
        return found;
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        PendingFiles().Count == 0 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"cleanup-targets={Targets.Count}");
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var before = PendingFiles();
        var deleted = 0;
        long bytes = 0L;
        foreach (var path in before)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var length = new FileInfo(path).Length;
                File.Delete(path);
                deleted++;
                bytes += length;
            }
            catch { /* en uso o sin acceso: se omite y se cuenta lo logrado */ }
        }

        _lastDeleted = deleted;
        return Task.FromResult(deleted > 0
            ? OperationResult.Ok($"Limpieza completada: {deleted} fichero(s), {bytes / 1024} KB liberados.")
            : OperationResult.Ok("No quedaban ficheros antiguos para limpiar."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La limpieza de ficheros no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastDeleted is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }

        // Verificación honesta de limpieza: el sistema regenera temporales y
        // hay ficheros en uso que se omiten por diseño. Éxito = progreso real
        // (se eliminó) o nada pendiente. Solo falla si no se movió nada.
        var remaining = PendingFiles().Count;
        if (remaining == 0)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} fichero(s) eliminados, nada pendiente."));
        }
        if (_lastDeleted > 0)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} fichero(s) eliminados; {remaining} restante(s) en uso o regenerados."));
        }
        return Task.FromResult(VerificationResult.Failed(OptimizationState.NotApplied,
            "No se pudo eliminar ningún fichero (en uso o sin acceso)."));
    }

    private int? _lastDeleted;

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Sin precondiciones específicas."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var pending = PendingFiles();
        var lines = Targets.Select(t => new PreviewLine
        {
            Kind = "Cleanup",
            Target = $"{ExpandDir(t.Directory)}\\{t.Pattern} (>{t.OlderThanDays}d)",
            Before = $"{pending.Count} fichero(s) antiguos",
            After = "eliminados",
        }).ToList();

        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines = lines,
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
    }
}
