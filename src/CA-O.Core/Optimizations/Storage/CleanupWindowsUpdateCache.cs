using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Limpia la caché de descargas de Windows Update
/// (%SystemRoot%\SoftwareDistribution\Download): detiene wuauserv+bits,
/// borra ficheros (nunca carpetas, omite en uso) y rearranca los servicios
/// pase lo que pase. La caché se re-descarga sola si hace falta.
/// Mantenimiento no reversible.
/// </summary>
public sealed class CleanupWindowsUpdateCache : IOptimization
{
    private int? _lastDeleted;
    private long _lastBytes;

    public OptimizationDefinition Definition => new()
    {
        Id = "cleanup-windows-update-cache",
        NameEs = "Limpiar caché de Windows Update",
        NameEn = "Clean Windows Update cache",
        DescriptionEs = "Vacía las descargas cacheadas de Windows Update (se re-descargan solas).",
        DescriptionEn = "Empties cached Windows Update downloads (re-downloaded if needed).",
        TooltipEs = "Detiene wuauserv y BITS, borra la caché y los rearranca siempre. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    internal static string CacheDirectory
    {
        get
        {
            try { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"); }
            catch { return @"C:\Windows\SoftwareDistribution\Download"; }
        }
    }

    internal static int CountPendingFiles(string dir)
    {
        try
        {
            return new DirectoryInfo(dir).Exists
                ? Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count()
                : 0;
        }
        catch { return 0; }
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        CountPendingFiles(CacheDirectory) == 0 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"wu-cache-files={CountPendingFiles(CacheDirectory)}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null)
            return OperationResult.Fail("Gestor de servicios no disponible.", "CAO-SEC-010");

        var stopped = new List<string>();
        foreach (var service in new[] { "wuauserv", "bits" })
        {
            try
            {
                if (context.Services.Exists(service))
                {
                    await context.Services.StopAsync(service, ct);
                    stopped.Add(service);
                }
            }
            catch (Exception ex)
            {
                await RestartServicesAsync(context.Services, stopped, CancellationToken.None);
                return OperationResult.Fail($"No se pudo detener {service}: {ex.Message}.", "stop-failed");
            }
        }

        var deleted = 0;
        long bytes = 0L;
        try
        {
            foreach (var path in Directory.EnumerateFiles(CacheDirectory, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    bytes += new FileInfo(path).Length;
                    File.Delete(path);
                    deleted++;
                }
                catch { /* en uso o sin acceso: se omite */ }
            }
        }
        finally
        {
            await RestartServicesAsync(context.Services, stopped, CancellationToken.None);
        }

        _lastDeleted = deleted;
        _lastBytes = bytes;
        return deleted > 0
            ? OperationResult.Ok($"Caché de Windows Update limpia: {deleted} fichero(s), {bytes / 1024 / 1024} MB liberados. Servicios rearrancados.")
            : OperationResult.Ok("La caché de Windows Update ya estaba vacía. Servicios rearrancados.");
    }

    private static async Task RestartServicesAsync(IServiceManager services, IReadOnlyList<string> stopped, CancellationToken ct)
    {
        foreach (var service in stopped)
        {
            try { await services.StartAsync(service, ct); }
            catch { }
        }
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La limpieza de caché no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastDeleted is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }
        var remaining = CountPendingFiles(CacheDirectory);
        if (remaining == 0)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} fichero(s) eliminados ({_lastBytes / 1024 / 1024} MB), caché vacía."));
        }
        if (_lastDeleted > 0)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} fichero(s) eliminados; {remaining} restante(s) en uso o regenerados."));
        }
        return Task.FromResult(VerificationResult.Failed(OptimizationState.NotApplied,
            "No se pudo eliminar ningún fichero (en uso o sin acceso)."));
    }
}
