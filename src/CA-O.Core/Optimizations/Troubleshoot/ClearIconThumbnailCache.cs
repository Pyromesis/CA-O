using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Clears icon/thumbnail caches in every local user profile.</summary>
public sealed class ClearIconThumbnailCache : IOptimization
{
    private const string ProfileListKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    public OptimizationDefinition Definition => new()
    {
        Id = "clear-icon-thumbnail-cache",
        NameEs = "Limpiar caché de iconos y miniaturas",
        NameEn = "Clear icon and thumbnail cache",
        DescriptionEs = "Borra las cachés de iconos y miniaturas de todos los perfiles. Corrige iconos rotos.",
        DescriptionEn = "Clears icon and thumbnail caches in every profile. Fixes broken icons.",
        TooltipEs = "Borra iconcache_*.db y thumbcache_*.db por perfil. Cierre sesión para regenerar.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    private IReadOnlyList<string> CacheFiles(IRegistryAccessor registry)
    {
        var found = new List<string>();
        IReadOnlyList<string> sids;
        try { sids = registry.GetSubKeyNames(RegistryHive2.LocalMachine, ProfileListKey); }
        catch { return found; }
        foreach (var sid in sids)
        {
            var profilePath = registry.GetValue(
                RegistryHive2.LocalMachine, $@"{ProfileListKey}\{sid}", "ProfileImagePath") as string;
            if (string.IsNullOrWhiteSpace(profilePath)) continue;
            var explorerDir = Path.Combine(profilePath, "AppData", "Local", "Microsoft", "Windows", "Explorer");
            DirectoryInfo info;
            try { info = new DirectoryInfo(explorerDir); }
            catch { continue; }
            if (!info.Exists) continue;
            FileInfo[] files;
            try { files = info.GetFiles("*.db", SearchOption.TopDirectoryOnly); }
            catch { continue; }
            foreach (var file in files)
            {
                var name = file.Name.ToLowerInvariant();
                if (name.StartsWith("iconcache_", StringComparison.Ordinal) ||
                    name.StartsWith("thumbcache_", StringComparison.Ordinal))
                {
                    found.Add(file.FullName);
                }
            }
        }
        return found;
    }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        CacheFiles(registry).Count == 0 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"icon-cache-files={CacheFiles(registry).Count}");
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var deleted = 0;
        foreach (var path in CacheFiles(context.Registry))
        {
            ct.ThrowIfCancellationRequested();
            try { File.Delete(path); deleted++; }
            catch { }
        }

        _lastDeleted = deleted;
        return Task.FromResult(deleted > 0
            ? OperationResult.Ok($"Cachés eliminadas: {deleted} fichero(s). Cierre sesión para regenerar.")
            : OperationResult.Ok("No quedaban cachés de iconos para limpiar."));
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

        // Explorer regenera cachés al instante y hay ficheros en uso:
        // éxito = progreso real o nada pendiente.
        var remaining = CacheFiles(context.Registry).Count;
        if (remaining == 0)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                "Cachés verificadas eliminadas."));
        }
        if (_lastDeleted > 0)
        {
            return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao,
                $"Verificado: {_lastDeleted} fichero(s) eliminados; {remaining} restante(s) en uso o regenerados."));
        }
        return Task.FromResult(VerificationResult.Failed(OptimizationState.NotApplied,
            "No se pudo eliminar ninguna caché (en uso)."));
    }

    private int? _lastDeleted;
}
