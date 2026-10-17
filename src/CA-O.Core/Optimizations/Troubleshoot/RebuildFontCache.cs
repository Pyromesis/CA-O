using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>
/// Letras que se ven como cuadros, fuentes que no aparecen o texto borroso
/// tras instalar fuentes: detiene el servicio de caché de fuentes, borra la
/// caché (FontCache\*.dat y FNTCACHE.DAT) y lo arranca. Windows la regenera
/// al volver a pintar texto; las fuentes instaladas no se tocan.
/// </summary>
public sealed class RebuildFontCache : IOptimization
{
    private const string FontCacheService = "FontCache";
    private bool? _lastOk;

    private static string WinDir
    {
        get
        {
            try { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); }
            catch { return @"C:\Windows"; }
        }
    }

    internal static string CacheDirectory =>
        Path.Combine(WinDir, "ServiceProfiles", "LocalService", "AppData", "Local", "FontCache");

    internal static string SystemCacheFile => Path.Combine(WinDir, "System32", "FNTCACHE.DAT");

    public OptimizationDefinition Definition => new()
    {
        Id = "rebuild-font-cache",
        NameEs = "Reconstruir caché de fuentes",
        NameEn = "Rebuild font cache",
        DescriptionEs = "Para letras como cuadros, fuentes que no aparecen o texto que se ve mal tras instalar fuentes.",
        DescriptionEn = "For text shown as boxes, missing fonts or garbled text after installing fonts.",
        TooltipEs = "Detiene FontCache, borra la caché de fuentes y lo vuelve a iniciar. Las fuentes instaladas no se tocan. Algunas apps pueden pedir reiniciarlas.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null)
            return OperationResult.Fail("Gestor de servicios no disponible.", "no-services");
        if (!context.Services.Exists(FontCacheService))
            return OperationResult.Fail("El servicio de caché de fuentes no existe en este equipo.", "service-not-found");

        try { await context.Services.StopAsync(FontCacheService, ct); }
        catch { }

        var deleted = 0;
        var skipped = 0;
        var files = new List<string>();
        try
        {
            if (Directory.Exists(CacheDirectory))
                files.AddRange(Directory.EnumerateFiles(CacheDirectory, "*.dat", SearchOption.TopDirectoryOnly));
        }
        catch { }
        if (File.Exists(SystemCacheFile)) files.Add(SystemCacheFile);

        foreach (var file in files)
        {
            try { File.Delete(file); deleted++; }
            catch { skipped++; }
        }

        try { await context.Services.StartAsync(FontCacheService, ct); }
        catch (Exception ex)
        {
            _lastOk = false;
            return OperationResult.Fail("La caché se borró pero el servicio no volvió a arrancar: reinicia el equipo.", ex.Message);
        }

        _lastOk = true;
        return OperationResult.Ok(deleted > 0
            ? $"Caché de fuentes reconstruida: {deleted} fichero(s) borrados{(skipped > 0 ? $", {skipped} en uso" : string.Empty)}. Reinicia las apps con texto raro."
            : "No había caché de fuentes que borrar; el servicio se reinició.");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La caché de fuentes se regenera sola: no hay nada que revertir."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default) =>
        Task.FromResult(_lastOk switch
        {
            null => VerificationResult.Unknown(OptimizationState.Unknown, "Sin evidencia de ejecución en esta sesión."),
            true => VerificationResult.Passed(OptimizationState.AppliedByCao, "Caché borrada y servicio iniciado."),
            false => VerificationResult.Failed(OptimizationState.NotApplied, "El servicio de fuentes no volvió a arrancar."),
        });
}
