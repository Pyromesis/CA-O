using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Limpia cachés de shaders compilados de GPU (NVIDIA DXCache/GLCache, AMD
/// DxCache, Intel ShaderCache): se regeneran solas al abrir cada juego.
/// Solo ficheros dentro de esas carpetas de caché; nunca drivers,
/// perfiles ni configuración. Solo se actúa a partir de 50 MB.
/// Mantenimiento no reversible.
/// </summary>
public sealed class CleanupGpuShaderCache : IOptimization
{
    /// <summary>Solo se actúa a partir de este total (evita nagging por MB).</summary>
    internal const long ThresholdBytes = 50L * 1024 * 1024;

    internal static readonly IReadOnlyList<(string BaseKind, string RelativeDir)> CacheDirs =
    [
        ("LocalAppData", @"NVIDIA\DXCache"),
        ("LocalAppData", @"NVIDIA\GLCache"),
        ("LocalAppData", @"AMD\DxCache"),
        ("LocalAppData", @"Intel\ShaderCache"),
    ];

    private int? _lastDeleted;
    private long _lastBytes;

    public OptimizationDefinition Definition => new()
    {
        Id = "cleanup-gpu-shader-cache",
        NameEs = "Limpiar caché de shaders GPU",
        NameEn = "Clean GPU shader cache",
        DescriptionEs = "Borra shaders compilados de NVIDIA, AMD e Intel (se regeneran al jugar).",
        DescriptionEn = "Deletes compiled NVIDIA, AMD and Intel shaders (regenerated when gaming).",
        TooltipEs = "Solo ficheros en DXCache/GLCache/DxCache/ShaderCache. Nunca drivers ni configuración. A partir de 50 MB. Mantenimiento no reversible.",
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

    internal static IReadOnlyList<string> ExistingDirs()
    {
        var found = new List<string>();
        string local;
        // CAO-BUG-2026-10-06 (F1): con la suplantacion perdida tras el primer
        // await (ver CallerContext), GetFolderPath devolvia el %LOCALAPPDATA%
        // de SYSTEM y la limpieza de shaders no encontraba la cache del usuario.
        try { local = CallerProfile.Folder(Environment.SpecialFolder.LocalApplicationData); }
        catch { return found; }
        if (string.IsNullOrWhiteSpace(local))
            return found;
        foreach (var (_, relative) in CacheDirs)
        {
            try
            {
                var dir = Path.Combine(local, relative);
                if (Directory.Exists(dir))
                    found.Add(dir);
            }
            catch { }
        }
        return found;
    }

    internal static long CacheBytes()
    {
        long bytes = 0;
        foreach (var dir in ExistingDirs())
            bytes += SafeFileEnumeration.BytesIn(dir);
        return bytes;
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        CacheBytes() >= ThresholdBytes ? OptimizationState.NotApplied : OptimizationState.AppliedByCao;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add("gpu-shader-cache=mantenimiento puntual (sin estado previo que guardar)");
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        int files = 0;
        long bytes = 0;
        foreach (var dir in ExistingDirs())
        {
            // CAO-BUG-2026-10-06: GetFiles(AllDirectories) abortaba la limpieza
            // entera en cuanto una subcarpeta denyaba el acceso, y materializaba
            // ademas el array completo en memoria. Ahora se enumera en streaming
            // con la enumeracion segura compartida.
            foreach (var path in SafeFileEnumeration.Files(dir))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var file = new FileInfo(path);
                    // Solo ficheros de caché: jamás se borran carpetas (la
                    // estructura la recrea el driver) ni nada fuera de
                    // estas 4 rutas. En uso → se omite.
                    bytes += file.Length;
                    file.Delete();
                    files++;
                }
                catch { /* en uso o sin acceso: se omite */ }
            }
        }

        _lastDeleted = files;
        _lastBytes = bytes;
        return Task.FromResult(files > 0
            ? OperationResult.Ok($"Caché de shaders limpia: {files} fichero(s), {bytes / 1024 / 1024} MB liberados. Se regenera al jugar.")
            : OperationResult.Ok("Sin caché de shaders que limpiar."));
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
        var remaining = CacheBytes();
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
            "No se pudo eliminar ningún fichero (en uso o sin acceso)."));
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        // CAO-BUG-2026-10-06: CacheBytes() se llamaba DENTRO del Select, o sea
        // una vez por cada carpeta de cache, y cada llamada ya recorria todas.
        // Ademas cada linea mostraba el total de TODAS las carpetas, no el de la
        // suya, de modo que la vista previa senalaba la carpeta equivocada.
        // Ahora cada linea mide la suya y solo se recorre una vez por carpeta.
        var lines = new List<PreviewLine>();
        foreach (var dir in ExistingDirs())
        {
            var ownBytes = SafeFileEnumeration.BytesIn(dir);
            lines.Add(new PreviewLine
            {
                Kind = "Cleanup",
                Target = dir,
                Before = $"{ownBytes / 1024 / 1024} MB en caché",
                After = "ficheros eliminados (solo caché)",
            });
        }
        if (lines.Count == 0)
        {
            lines.Add(new PreviewLine
            {
                Kind = "Cleanup",
                Target = "%LOCALAPPDATA%\\{NVIDIA\\DXCache, NVIDIA\\GLCache, AMD\\DxCache, Intel\\ShaderCache}",
                Before = "sin carpetas de caché presentes",
                After = "sin cambios",
            });
        }
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
