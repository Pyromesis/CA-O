using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Storage;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Verify honesto de limpiadores: el sistema regenera ficheros y hay
/// bloqueados en uso. Éxito = progreso real o nada pendiente; solo falla
/// si no se movió nada (antes: cualquier resto => CAO-TXN-003).
/// Retención de la familia: 10 minutos (frontera 11 cae / 9 se respeta).
/// </summary>
public sealed class CleanupVerifyTests : IDisposable
{
    private readonly string _dir;
    private readonly List<FileStream> _locks = [];

    public CleanupVerifyTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cao-cleanup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        foreach (var s in _locks) s.Dispose();
        _locks.Clear();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private sealed class ScratchCleanup(string dir) : TempFileCleanupOptimization
    {
        public override OptimizationDefinition Definition => new()
        {
            Id = "test-scratch-cleanup",
            NameEs = "Prueba",
            NameEn = "Test",
            DescriptionEs = "Limpieza de prueba",
            DescriptionEn = "Test cleanup",
            Evidence = EvidenceLevel.Benchmark,
            Risk = RiskLevel.Safe,
            Compatibility = CompatibilityStatus.Compatible,
            SecurityImpact = SecurityImpact.None,
        };

        protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets { get; } =
        [
            (dir, "*.*", 10),
        ];
    }

    /// <summary>Objetivo con nombre de fichero concreto (p.ej. MEMORY.DMP):
    /// solo existe en el propio directorio, nunca en sus subdirectorios.</summary>
    private sealed class ExactNameCleanup(string dir, string name) : TempFileCleanupOptimization
    {
        public override OptimizationDefinition Definition => new()
        {
            Id = "test-exact-name-cleanup",
            NameEs = "Prueba nombre exacto",
            NameEn = "Test exact name",
            DescriptionEs = "Limpieza de prueba con nombre concreto",
            DescriptionEn = "Test cleanup with an exact file name",
            Evidence = EvidenceLevel.Benchmark,
            Risk = RiskLevel.Safe,
            Compatibility = CompatibilityStatus.Compatible,
            SecurityImpact = SecurityImpact.None,
        };

        protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanMinutes)> Targets { get; } =
        [
            (dir, name, 10),
        ];
    }

    private string WriteFile(string name, int ageMinutes, bool lockExclusive = false)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-ageMinutes));
        if (lockExclusive)
        {
            _locks.Add(new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
        }
        return path;
    }

    private static OptimizationContext Context(MemoryRegistry registry) =>
        new() { Registry = registry };

    /// <summary>Frontera de retención (10 min) por las tres vías: Detect ve el
    /// fichero de 11 min como pendiente, Apply lo borra y Verify pasa; el de
    /// 9 min no es pendiente en ninguna vía y sobrevive al Apply.</summary>
    [Fact]
    public async Task ElevenMinutes_Falls_NineMinutes_Survives_OnEveryPath()
    {
        WriteFile("eleven.txt", 11);
        WriteFile("nine.txt", 9);
        var opt = new ScratchCleanup(_dir);

        Assert.Equal(OptimizationState.NotApplied, opt.Detect(new MemoryRegistry()));

        var apply = await opt.ApplyAsync(Context(new MemoryRegistry()));
        Assert.True(apply.Success);
        Assert.False(File.Exists(Path.Combine(_dir, "eleven.txt")), "11 min >= corte: debe caer.");
        Assert.True(File.Exists(Path.Combine(_dir, "nine.txt")), "9 min < corte: debe respetarse.");

        Assert.Equal(OptimizationState.AppliedByCao, opt.Detect(new MemoryRegistry()));
        var verify = await opt.VerifyAsync(Context(new MemoryRegistry()));
        Assert.Equal(VerificationStatus.Passed, verify.Status);
    }

    [Fact]
    public async Task PartialDelete_Passes_WithRemainingNote()
    {
        WriteFile("old1.txt", 120);
        WriteFile("old2.txt", 120);
        WriteFile("locked.txt", 120, lockExclusive: true);
        WriteFile("fresh.txt", 0);
        var opt = new ScratchCleanup(_dir);

        var apply = await opt.ApplyAsync(Context(new MemoryRegistry()));
        Assert.True(apply.Success);

        var verify = await opt.VerifyAsync(Context(new MemoryRegistry()));
        Assert.Equal(VerificationStatus.Passed, verify.Status);
        Assert.Contains("restante", verify.MessageEs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NothingDeletable_Fails_Honestly()
    {
        WriteFile("locked.txt", 120, lockExclusive: true);
        var opt = new ScratchCleanup(_dir);

        var apply = await opt.ApplyAsync(Context(new MemoryRegistry()));
        Assert.True(apply.Success);

        var verify = await opt.VerifyAsync(Context(new MemoryRegistry()));
        Assert.Equal(VerificationStatus.Failed, verify.Status);
    }

    [Fact]
    public async Task EmptyDir_Passes()
    {
        var opt = new ScratchCleanup(_dir);

        var apply = await opt.ApplyAsync(Context(new MemoryRegistry()));
        Assert.True(apply.Success);

        var verify = await opt.VerifyAsync(Context(new MemoryRegistry()));
        Assert.Equal(VerificationStatus.Passed, verify.Status);
        Assert.Equal(OptimizationState.AppliedByCao, opt.Detect(new MemoryRegistry()));
    }

    [Fact]
    public async Task ExactFileName_IsResolvedInItsOwnDirectory_NotRecursively()
    {
        // CAO-BUG-2026-10-06: cleanup-crash-dumps-extended declara
        // (%SystemRoot%, "MEMORY.DMP", 30). Con SearchOption.AllDirectories
        // ese unico fichero obligaba a recorrer TODO C:\Windows (WinSxS
        // incluido) cuatro o cinco veces por cada apply.
        var nested = Path.Combine(_dir, "nested");
        Directory.CreateDirectory(nested);
        var rootFile = WriteFile("MEMORY.DMP", 120);
        var nestedFile = Path.Combine(nested, "MEMORY.DMP");
        File.WriteAllText(nestedFile, "x");
        File.SetLastWriteTimeUtc(nestedFile, DateTime.UtcNow.AddMinutes(-120));
        var opt = new ExactNameCleanup(_dir, "MEMORY.DMP");

        var apply = await opt.ApplyAsync(Context(new MemoryRegistry()));

        Assert.True(apply.Success, apply.MessageEs);
        Assert.False(File.Exists(rootFile), "El fichero del directorio objetivo debe eliminarse.");
        Assert.True(
            File.Exists(nestedFile),
            "Un nombre de fichero concreto no debe Localizarse dentro de subdirectorios.");
    }

    [Fact]
    public async Task EmptySubdirs_AreRemoved_WithTheFiles()
    {
        var emptied = Path.Combine(_dir, "emptied");
        Directory.CreateDirectory(emptied);
        var inner = Path.Combine(emptied, "old.txt");
        File.WriteAllText(inner, "x");
        File.SetLastWriteTimeUtc(inner, DateTime.UtcNow.AddMinutes(-120));
        var alreadyEmpty = Path.Combine(_dir, "already-empty");
        Directory.CreateDirectory(alreadyEmpty);
        WriteFile("fresh.txt", 0);
        var opt = new ScratchCleanup(_dir);

        var apply = await opt.ApplyAsync(Context(new MemoryRegistry()));

        Assert.True(apply.Success, apply.MessageEs);
        Assert.False(File.Exists(inner));
        Assert.False(Directory.Exists(emptied), "La carpeta que quedo vacia debe eliminarse.");
        Assert.False(Directory.Exists(alreadyEmpty), "La carpeta ya vacia debe eliminarse.");
        Assert.True(File.Exists(Path.Combine(_dir, "fresh.txt")));
        Assert.Contains("carpeta", apply.MessageEs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Subdirs_WithRecentFiles_AreKept()
    {
        var kept = Path.Combine(_dir, "kept");
        Directory.CreateDirectory(kept);
        var recent = Path.Combine(kept, "recent.txt");
        File.WriteAllText(recent, "x");
        var opt = new ScratchCleanup(_dir);

        var apply = await opt.ApplyAsync(Context(new MemoryRegistry()));

        Assert.True(apply.Success, apply.MessageEs);
        Assert.True(Directory.Exists(kept));
        Assert.True(File.Exists(recent));
    }
}
