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

    /// <summary>Subdirectorios existentes bajo cada perfil de C:\Users. El servicio
    /// corre como SYSTEM y %TEMP% solo le da el suyo: así se alcanza el Temp real
    /// de cada usuario. Solo rutas absolutas existentes bajo \Users (nunca se
    /// elevan rutas de usuario sin validar).</summary>
    internal static IReadOnlyList<string> ProfileSubDirs(params string[] relativeParts)
    {
        var found = new List<string>();
        try
        {
            foreach (var profile in ProfileRoots())
            {
                try
                {
                    var dir = profile;
                    foreach (var part in relativeParts) dir = Path.Combine(dir, part);
                    if (Directory.Exists(dir) && dir.Length > 3) found.Add(dir);
                }
                catch { }
            }
        }
        catch { }
        return found;
    }

    private static readonly object ProfileCacheGate = new();
    private static string[]? _profileRoots;
    private static DateTime _profileRootsStampUtc;

    /// <summary>Enumerador de los perfiles de usuario. Sustituible en pruebas.</summary>
    internal static Func<string, string[]> ProfileRootScanner { get; set; } = Directory.GetDirectories;

    /// <summary>
    /// Antigüedad máxima del listado de perfiles cacheado.
    /// CAO-BUG-2026-10-06: cuatro subclases de la familia declaran
    /// <c>Targets =&gt; BuildTargets()</c>, así que la lista de objetivos se
    /// reconstruye en cada acceso a la propiedad, y cada lectura ejecutaba
    /// <c>Directory.GetDirectories("C:\Users")</c> una vez por parte de la ruta.
    /// Para <c>cleanup-windows-temp</c> son cinco enumeraciones por lectura,
    /// repetidas en cada apply, verify y análisis. El TTL es corto a propósito:
    /// dentro de una misma operación el resultado se usa tal cual, y un perfil
    /// nuevo se detecta en menos de un minuto.
    /// </summary>
    internal static TimeSpan ProfileCacheTtl = TimeSpan.FromSeconds(60);

    /// <summary>Vacía la caché de perfiles (uso interno y pruebas).</summary>
    internal static void ResetProfileCache()
    {
        lock (ProfileCacheGate)
        {
            _profileRoots = null;
            _profileRootsStampUtc = default;
        }
    }

    private static IReadOnlyList<string> ProfileRoots()
    {
        var now = DateTime.UtcNow;
        lock (ProfileCacheGate)
        {
            if (_profileRoots is not null && now - _profileRootsStampUtc < ProfileCacheTtl)
                return _profileRoots;

            var usersRoot = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Users");
            _profileRoots = Directory.Exists(usersRoot)
                ? ProfileRootScanner(usersRoot)
                : Array.Empty<string>();
            _profileRootsStampUtc = now;
            return _profileRoots;
        }
    }

    /// <summary>Temp de cada usuario interactivo (AppData\Local\Temp existente).</summary>
    internal static IReadOnlyList<string> InteractiveUserTempDirs() =>
        ProfileSubDirs("AppData", "Local", "Temp");

    /// <summary>Formato de tamaño para mensajes. Base = KB (mensajes históricos intactos).</summary>
    protected virtual string FormatSize(long bytes) => $"{bytes / 1024} KB";

    private IReadOnlyList<string> PendingFiles() => PendingFiles(out _);

    private IReadOnlyList<string> PendingFiles(out bool anyTargetUnreadable)
    {
        var found = new List<string>();
        var anyUnreadable = false;
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
            // Recursivo: %TEMP% acumula el grueso en subcarpetas (VS, Edge,
            // instaladores). TopDirectoryOnly dejaba casi todo sin tocar.
            List<string> candidates = new();
            try
            {
                foreach (var p in EnumerateTarget(dir, pattern))
                    candidates.Add(p);
            }
            catch
            {
                // Sin acceso a alguna subcarpeta: se limpia con lo listado
                // hasta el fallo. Si no se listó nada, se intenta el nivel
                // superior para no dejar el directorio sin cubrir.
                anyUnreadable = true;
                if (candidates.Count == 0)
                {
                    try
                    {
                        foreach (var p in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                            candidates.Add(p);
                    }
                    catch { continue; }
                }
            }
            foreach (var fullName in candidates)
            {
                FileInfo file;
                try { file = new FileInfo(fullName); }
                catch { continue; }
                DateTime stamp;
                try { stamp = file.LastWriteTimeUtc; }
                catch { continue; }
                if (stamp <= cutoff) found.Add(file.FullName);
            }
        }
        anyTargetUnreadable = anyUnreadable;
        return found;
    }

    /// <summary>
    /// Enumeracion de un objetivo (CAO-BUG-2026-10-06).
    /// Un patron sin comodin (p.ej. MEMORY.DMP) se resuelve en el propio
    /// directorio: con SearchOption.AllDirectories ese unico fichero obligaba
    /// a recorrer TODO C:\Windows (WinSxS incluido) cuatro o cinco veces por
    /// cada apply. Los patrones con comodin conservan la recursion porque el
    /// grueso de %TEMP% vive en subcarpetas (VS, Edge, instaladores), pero
    /// con EnumerationOptions: IgnoreInaccessible evita que una subcarpeta sin
    /// acceso trunque la lista en silencio (falsos "ya aplicado") y
    /// AttributesToSkip impide bucles infinitos siguiendo junctions.
    /// </summary>
    private static IEnumerable<string> EnumerateTarget(string dir, string pattern)
    {
        if (!pattern.Contains('*') && !pattern.Contains('?'))
            return Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly);

        return Directory.EnumerateFiles(dir, pattern, new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        });
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        // CAO-BUG-2026-10-06: Targets se filtra por existencia (ProfileSubDirs solo
        // devuelve directorios reales), asi que en una maquina sin la aplicacion
        // asociada el conjunto queda vacio. Con cero objetivos no se ha
        // inspeccionado nada y la respuesta honesta es "no aplicado": antes se
        // contestaba AppliedByCao y la transaccion decia "Ya aplicado y
        // verificado, no se puede volver a aplicar", dejando la limpieza como un
        // no-op permanente con apariencia de exito. No se devuelve Unknown a
        // proposito: SafeDetect lo traduce a RollbackRequired y abriria el
        // bloqueo de recuperacion.
        if (Targets.Count == 0)
            return OptimizationState.NotApplied;

        var pending = PendingFiles(out var unreadable);
        // CAO-BUG-2026-10-06: si no se pudo leer algun objetivo no se puede
        // afirmar que este limpio. Antes un directorio sin acceso producia
        // AppliedByCao, la transaccion contestaba "ya aplicado y verificado"
        // sin haber borrado nada y el id quedaba bloqueado para siempre.
        // NotApplied es la respuesta conservadora: se intenta limpiar.
        if (pending.Count == 0 && unreadable) return OptimizationState.NotApplied;
        return pending.Count == 0 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;
    }

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
            ? OperationResult.Ok($"Limpieza completada: {deleted} fichero(s), {FormatSize(bytes)} liberados.")
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
