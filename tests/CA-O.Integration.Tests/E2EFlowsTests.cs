using CAO.Core.Abstractions;
using CAO.Core.Diagnostics;
using CAO.Infrastructure.Benchmarking;
using CAO.Infrastructure.Persistence;
using CAO.Infrastructure.Services;
using CAO.Shared;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CAO.Integration.Tests;

/// <summary>E2E §80: 10 flujos reales sin UI automation (servicios directos) — cada uno debe pasar aislado.</summary>
[Collection("E2E")]
public sealed class E2EFlowsTests
{
    private static IAnalysisCoordinator NewAnalysisService(string dir)
    {
        var store = new AnalysisStateStore(Path.Combine(dir, "analysis.json"));
        var provider = new StubContextProvider();
        var registry = new StubRegistry();
        return new SystemAnalysisService(provider, store, registry);
    }
    private sealed class StubContextProvider : ISystemContextProvider
    {
        public Task<SystemContext> GetAsync(CancellationToken ct = default) => Task.FromResult(SystemContextFactory.Default());
    }
    private sealed class StubRegistry : IRegistryAccessor
    {
        public RegistryValueKind2 GetKind(RegistryHive2 h, string p, string n) => RegistryValueKind2.None;
        public object? GetValue(RegistryHive2 h, string p, string n) => null;
        public object? GetValueRaw(RegistryHive2 h, string p, string n, out RegistryValueKind2 k) { k = RegistryValueKind2.None; return null; }
        public void SetValue(RegistryHive2 h, string p, string n, object v, RegistryValueKind2 k) { }
        public void SetValueRaw(RegistryHive2 h, string p, string n, object v, RegistryValueKind2 k) { }
        public bool DeleteValue(RegistryHive2 h, string p, string n) => false;
        public IReadOnlyList<string> GetValueNames(RegistryHive2 h, string p) => Array.Empty<string>();
    }
    private static string NewTempDir() => Directory.CreateTempSubdirectory($"cao-e2e-{Guid.NewGuid():N}").FullName;

    [Fact] // Caso 1: Abrir -> Dashboard visible con estado previo si existe
    public async Task Caso1_Abrir_MuestraEstadoPrevio()
    {
        var dir = NewTempDir();
        var svc = NewAnalysisService(dir);
        var store = new AnalysisStateStore(Path.Combine(dir, "analysis.json"));
        // Simular análisis previo persistido
        var result = await svc.RunAsync();
        Assert.Equal("Completed", result.AnalysisState);
        var loaded = store.LoadLatestAnalysis();
        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.Context);
        // "Reabrir": nuevo store debe ver mismo análisis
        var store2 = new AnalysisStateStore(Path.Combine(dir, "analysis.json"));
        var reloaded = store2.LoadLatestAnalysis();
        Assert.NotNull(reloaded);
        Assert.Equal(loaded.TimestampUtc, reloaded!.TimestampUtc);
    }

    [Fact] // Caso 2: Analizar -> persistir -> cerrar y reabrir sigue visible
    public async Task Caso2_Analizar_Persiste_Y_Reabre()
    {
        var dir = NewTempDir();
        var svc = NewAnalysisService(dir);
        var before = await svc.RunAsync();
        var store = new AnalysisStateStore(Path.Combine(dir, "analysis.json"));
        var persisted = store.LoadLatestAnalysis();
        Assert.NotNull(persisted);
        Assert.True(persisted!.Recommendations?.Count > 0);
        // Simular cierre: recrear servicio y verificar que Health sigue calculable
        var health = HealthEngine.Evaluate(persisted.Context);
        Assert.True(health.Scores.Count > 0);
    }

    [Fact] // Caso 5: History corrupto -> app no cierra, warning visible
    public void Caso5_HistoryCorrupto_NoCierra()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cao-e2e-hist-{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(path, "{ not json\n");
        File.AppendAllText(path, "{\"TimestampUtc\":\"2026-08-26T10:00:00Z\",\"OptimizationId\":\"x\",\"Operation\":\"apply\",\"Success\":true}\n");
        var logger = new CAO.Infrastructure.Logging.JsonHistoryLogger(path);
        var last = logger.ReadLast(10);
        var warnings = logger.VerifyIntegrity();
        Assert.True(last.Count >= 1);
        Assert.True(warnings.Count >= 1);
    }

    [Fact] // Caso 6: Service detenido -> modo degradado (diagnostico sigue)
    public async Task Caso6_ServiceDetenido_ModoDegradado()
    {
        var dir = NewTempDir();
        var svc = NewAnalysisService(dir);
        // Sin servicio, el análisis (solo lectura) debe seguir funcionando
        var result = await svc.RunAsync();
        Assert.NotNull(result.Context);
        Assert.True(result.Modules.Count >= 4);
    }

    [Fact] // Caso 7: Vanguard detectado -> VBS bloqueado
    public void Caso7_Vanguard_BloqueaVbs()
    {
        var ctx = SystemContextFactory.Default() with { AntiCheats = new[] { new AntiCheatInfo(AntiCheatKind.Vanguard, "svc", new[] { "vgc" }) } };
        Assert.True(CAO.Core.Gaming.GameCompatibilityPolicy.IsBlocked("disable-vbs", ctx));
        Assert.False(CAO.Core.Gaming.GameCompatibilityPolicy.IsBlocked("disable-transparency", ctx));
    }

    [Fact] // Caso 8: Restore -> snapshots reales aparecen
    public async Task Caso8_Restore_SnapshotsReales()
    {
        var dir = NewTempDir();
        var store = new CAO.Infrastructure.Persistence.FileSnapshotStore(Path.Combine(dir, "snapshots"));
        var repo = new CAO.Infrastructure.Persistence.SnapshotRepository(store);
        var tx = Guid.NewGuid();
        var rec = new CAO.Core.Rollback.TransactionSnapshotRecord { Manifest = new CAO.Core.Rollback.TransactionSnapshotManifest { TransactionId = tx, OptimizationId = "test-opt", DefinitionVersion = "1", SchemaVersion = 3, AppVersion = "2.0.0", WindowsBuild = 26200, TimestampUtc = DateTime.UtcNow }, State = new OptimizationSnapshot() };
        store.Save(rec);
        var all = await repo.GetAllSnapshotsAsync();
        Assert.Single(all);
        Assert.Equal(tx, all[0].Manifest.TransactionId);
    }

    [Fact] // Caso 9: History -> no crash
    public void Caso9_History_NoCrash()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cao-e2e-hist2-{Guid.NewGuid():N}.jsonl");
        var logger = new CAO.Infrastructure.Logging.JsonHistoryLogger(path);
        // Vacío debe ser seguro
        Assert.Empty(logger.ReadLast(10));
        logger.Log(new HistoryEntry { TimestampUtc = DateTime.UtcNow, AppVersion = "2.0.0", OptimizationId = "x", Operation = "apply", Success = true });
        Assert.Single(logger.ReadLast(10));
    }

    [Fact] // Caso 10: Benchmark -> baseline/after workflow (dummy sin I/O para E2E determinista)
    public void Caso10_Benchmark_BaselineAfter()
    {
        var header1 = new BenchmarkRunHeader("baseline", DateTime.UtcNow, 26200, "", "test", 60, "ac");
        var header2 = new BenchmarkRunHeader("after", DateTime.UtcNow, 26200, "", "test", 60, "ac");
        var baseline = new SystemBenchmarkResult(header1, 1000, 20, 500, 400, TimeSpan.FromSeconds(1));
        var after = new SystemBenchmarkResult(header2, 1050, 21, 510, 410, TimeSpan.FromSeconds(1));
        var cmp = SystemBenchmarkRunner.Compare(baseline, after);
        Assert.False(string.IsNullOrWhiteSpace(cmp.VerdictEs));
        Assert.True(cmp.CpuDeltaPercent > 0);
    }
}
