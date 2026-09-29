using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Limpia los instaladores que GeForce Experience deja en
/// %ProgramData%\NVIDIA Corporation\Downloader tras cada actualización
/// de driver (acumulan GB). Solo ficheros de esa carpeta; nunca drivers
/// instalados ni configuración. Mantenimiento no reversible.
/// </summary>
public sealed class CleanupNvidiaDownloaderCache : IOptimization
{
    internal const string RelativeDir = @"NVIDIA Corporation\Downloader";

    private int? _lastDeleted;
    private long _lastBytes;

    public OptimizationDefinition Definition => new()
    {
        Id = "cleanup-nvidia-downloader-cache",
        NameEs = "Limpiar caché de descargas NVIDIA",
        NameEn = "Clean NVIDIA downloader cache",
        DescriptionEs = "Borra instaladores viejos de GeForce Experience (re-descargables).",
        DescriptionEn = "Deletes stale GeForce Experience installers (re-downloadable).",
        TooltipEs = "Solo ficheros en la carpeta de descargas de NVIDIA (ProgramData). Nunca drivers instalados ni configuración. Mantenimiento no reversible.",
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

    internal static string DownloaderDir()
    {
        try
        {
            var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return string.IsNullOrWhiteSpace(common) ? string.Empty : Path.Combine(common, RelativeDir);
        }
        catch { return string.Empty; }
    }

    internal static (int Files, long Bytes) Pending()
    {
        var dir = DownloaderDir();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return (0, 0);
        int files = 0;
        long bytes = 0;
        try
        {
            foreach (var file in new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { bytes += file.Length; files++; }
                catch { }
            }
        }
        catch { }
        return (files, bytes);
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        Pending().Files == 0 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add("nvidia-downloader=mantenimiento puntual (sin estado previo que guardar)");
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var dir = DownloaderDir();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return Task.FromResult(OperationResult.Ok("Sin carpeta de descargas NVIDIA en este equipo."));

        FileInfo[] candidates;
        try { candidates = new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories); }
        catch { return Task.FromResult(OperationResult.Fail("No se pudo listar la carpeta de descargas NVIDIA.", "list-failed")); }

        int files = 0;
        long bytes = 0;
        foreach (var file in candidates)
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

        _lastDeleted = files;
        _lastBytes = bytes;
        return Task.FromResult(files > 0
            ? OperationResult.Ok($"Descargas NVIDIA limpias: {files} fichero(s), {bytes / 1024 / 1024} MB liberados.")
            : OperationResult.Ok("Sin descargas NVIDIA pendientes de limpiar."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La limpieza de descargas no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastDeleted is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }
        var (files, _) = Pending();
        if (files == 0)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} fichero(s) eliminados ({_lastBytes / 1024 / 1024} MB), nada pendiente."));
        }
        if (_lastDeleted > 0)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} fichero(s) eliminados; restan {files} (en uso o regenerados)."));
        }
        return Task.FromResult(VerificationResult.Failed(OptimizationState.NotApplied,
            "No se pudo eliminar ningún fichero (en uso o sin acceso)."));
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var (files, bytes) = Pending();
        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Cleanup",
                    Target = Path.Combine("%ProgramData%", RelativeDir),
                    Before = $"{files} fichero(s), {bytes / 1024 / 1024} MB",
                    After = "eliminados",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
    }
}
