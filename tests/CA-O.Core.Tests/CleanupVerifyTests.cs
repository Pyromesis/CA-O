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

        protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; } =
        [
            (dir, "*.*", 1),
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

        protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; } =
        [
            (dir, name, 1),
        ];
    }

    private string WriteFile(string name, int ageDays, bool lockExclusive = false)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-ageDays));
        if (lockExclusive)
        {
            _locks.Add(new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
        }
        return path;
    }

    private static OptimizationContext Context(MemoryRegistry registry) =>
        new() { Registry = registry };

    [Fact]
    public async Task PartialDelete_Passes_WithRemainingNote()
    {
        WriteFile("old1.txt", 2);
        WriteFile("old2.txt", 2);
        WriteFile("locked.txt", 2, lockExclusive: true);
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
        WriteFile("locked.txt", 2, lockExclusive: true);
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
        var rootFile = WriteFile("MEMORY.DMP", 2);
        var nestedFile = Path.Combine(nested, "MEMORY.DMP");
        File.WriteAllText(nestedFile, "x");
        File.SetLastWriteTimeUtc(nestedFile, DateTime.UtcNow.AddDays(-2));
        var opt = new ExactNameCleanup(_dir, "MEMORY.DMP");

        var apply = await opt.ApplyAsync(Context(new MemoryRegistry()));

        Assert.True(apply.Success, apply.MessageEs);
        Assert.False(File.Exists(rootFile), "El fichero del directorio objetivo debe eliminarse.");
        Assert.True(
            File.Exists(nestedFile),
            "Un nombre de fichero concreto no debe Localizarse dentro de subdirectorios.");
    }
}
