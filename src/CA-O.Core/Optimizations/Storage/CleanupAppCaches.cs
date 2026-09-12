using System.Diagnostics;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Limpia cachés gordas de apps Electron/CEF (Discord, Spotify, Slack): solo
/// Cache/Code Cache/GPUCache (+Browser en Spotify). NUNCA Local Storage ni
/// IndexedDB (ahí vive la sesión: borrarlos desloguea) ni la música offline
/// de Spotify (Storage). Si la app está abierta se omite con aviso: hay que
/// cerrarla primero. Mantenimiento no reversible.
/// </summary>
public sealed class CleanupAppCaches : IOptimization
{
    /// <summary>Solo se actúa a partir de este total (evita nagging por MB).</summary>
    internal const long ThresholdBytes = 50L * 1024 * 1024;

    internal sealed record AppCacheTarget(
        string App,
        string[] ProcessNames,
        Environment.SpecialFolder BaseKind,
        string AppDir,
        string[] CacheSubdirs);

    internal static readonly IReadOnlyList<AppCacheTarget> Targets = new[]
    {
        new AppCacheTarget("Discord",
            ["Discord", "DiscordPTB", "DiscordCanary"],
            Environment.SpecialFolder.ApplicationData, "discord",
            ["Cache", "Code Cache", "GPUCache"]),
        // Spotify: Browser (CEF) + Data (legacy). Storage NO: ahí vive la
        // música offline descargada (se re-descarga, pero cuesta datos).
        new AppCacheTarget("Spotify",
            ["Spotify"],
            Environment.SpecialFolder.LocalApplicationData, "Spotify",
            ["Browser", "Data"]),
        new AppCacheTarget("Slack",
            ["slack"],
            Environment.SpecialFolder.ApplicationData, "Slack",
            ["Cache", "Code Cache", "GPUCache", Path.Combine("Service Worker", "CacheStorage")]),
    };

    private int? _lastDeleted;
    private long _lastBytes;

    public OptimizationDefinition Definition => new()
    {
        Id = "cleanup-app-caches",
        NameEs = "Limpiar cachés de apps",
        NameEn = "Clean app caches",
        DescriptionEs = "Vacía cachés de Discord, Spotify y Slack (GB de medios vistos).",
        DescriptionEn = "Empties Discord, Spotify and Slack caches (GBs of seen media).",
        TooltipEs = "Solo carpetas Cache/Code Cache/GPUCache (+Browser en Spotify). No toca sesiones ni música offline. Cierra cada app primero; si está abierta se omite. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Vendor,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    internal static string BaseDirectory(AppCacheTarget target)
    {
        try { return Path.Combine(Environment.GetFolderPath(target.BaseKind), target.AppDir); }
        catch { return string.Empty; }
    }

    internal static long CacheBytesFor(string baseDir, IReadOnlyList<string> subdirs)
    {
        long bytes = 0;
        if (string.IsNullOrWhiteSpace(baseDir)) return 0;
        foreach (var sub in subdirs)
        {
            try
            {
                var dir = new DirectoryInfo(Path.Combine(baseDir, sub));
                if (!dir.Exists) continue;
                foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try { bytes += file.Length; }
                    catch { }
                }
            }
            catch { }
        }
        return bytes;
    }

    internal static (int Files, long Bytes) DeleteCacheFiles(string baseDir, IReadOnlyList<string> subdirs, CancellationToken ct)
    {
        int files = 0;
        long bytes = 0;
        if (string.IsNullOrWhiteSpace(baseDir)) return (0, 0);
        foreach (var sub in subdirs)
        {
            try
            {
                var dir = new DirectoryInfo(Path.Combine(baseDir, sub));
                if (!dir.Exists) continue;
                foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        bytes += file.Length;
                        file.Delete();
                        files++;
                    }
                    catch { /* en uso o sin acceso: se omite */ }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }
        return (files, bytes);
    }

    internal static bool AnyProcessRunning(IEnumerable<string> names, Func<IEnumerable<string>>? listProcesses = null)
    {
        var running = listProcesses is not null
            ? listProcesses()
            : Process.GetProcesses().Select(p => { try { return p.ProcessName; } catch { return string.Empty; } });
        var set = new HashSet<string>(running.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
        return names.Any(n => set.Contains(n));
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        long total = 0;
        foreach (var target in Targets)
            total += CacheBytesFor(BaseDirectory(target), target.CacheSubdirs);
        return total >= ThresholdBytes ? OptimizationState.NotApplied : OptimizationState.AppliedByCao;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add("app-caches=mantenimiento puntual (sin estado previo que guardar)");
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var parts = new List<string>();
        int totalFiles = 0;
        long totalBytes = 0;
        foreach (var target in Targets)
        {
            ct.ThrowIfCancellationRequested();
            string baseDir;
            try { baseDir = BaseDirectory(target); }
            catch { continue; }
            if (AnyProcessRunning(target.ProcessNames))
            {
                parts.Add($"{target.App}: omitido (cierra {target.App} primero)");
                continue;
            }
            var (files, bytes) = DeleteCacheFiles(baseDir, target.CacheSubdirs, ct);
            if (files > 0)
            {
                parts.Add($"{target.App}: {files} fichero(s), {bytes / 1024 / 1024} MB");
                totalFiles += files;
                totalBytes += bytes;
            }
            else
            {
                parts.Add($"{target.App}: sin caché");
            }
        }

        _lastDeleted = totalFiles;
        _lastBytes = totalBytes;
        return Task.FromResult(totalFiles > 0
            ? OperationResult.Ok($"Cachés de apps limpias: {totalFiles} fichero(s), {totalBytes / 1024 / 1024} MB. ({string.Join(" · ", parts)})")
            : OperationResult.Ok("Sin caché de apps que limpiar. " + string.Join(" · ", parts)));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La limpieza de cachés no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastDeleted is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }
        long remaining = 0;
        foreach (var target in Targets)
            remaining += CacheBytesFor(BaseDirectory(target), target.CacheSubdirs);
        if (remaining < ThresholdBytes)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} fichero(s) eliminados ({_lastBytes / 1024 / 1024} MB), resto bajo umbral."));
        }
        if (_lastDeleted > 0)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} fichero(s) eliminados; restan {remaining / 1024 / 1024} MB (en uso o regenerados)."));
        }
        return Task.FromResult(VerificationResult.Failed(OptimizationState.NotApplied,
            "No se pudo eliminar ningún fichero (apps abiertas o sin acceso)."));
    }
}
