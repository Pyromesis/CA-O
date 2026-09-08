using CAO.Infrastructure.Persistence;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// La foto de Analizar sobrevive a disco (cierre de app) y a sesión
/// (cambio de pestaña vía UiState): textos, resolvers, filas y DPC.
/// Campo opcional: ficheros viejos sin Display se leen igual (sin bump).
/// </summary>
public sealed class DisplaySnapshotTests : IDisposable
{
    private readonly string _dir;

    public DisplaySnapshotTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cao-display-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static AnalysisDisplaySnapshot Sample() => new(
        Network: "Network: persistido",
        Security: null,
        Storage: "C: libre",
        Drivers: null,
        Input: "ratón",
        Thermal: null,
        Perf: null,
        DnsBest: "Mejor: 1.1.1.1",
        DnsPrimary: "1.1.1.1",
        DnsPrimaryMs: 12.3,
        DnsSecondary: "8.8.8.8",
        DnsSecondaryMs: 18.7,
        DnsRows: [new DnsRowSnapshot("1.1.1.1", 12.3)],
        StorageRows: [new StorageRowSnapshot("C:", 60.5, 385.1)],
        Interrupts: "ventana 5s",
        DpcStatus: "Baja",
        DpcMax: 2.5,
        GamingScan: "0 bloqueadas",
        GamingBlocked: null,
        GamingGames: "ninguno");

    private static AnalysisStateStore.PersistedAnalysis SamplePersisted(AnalysisDisplaySnapshot? display) =>
        new(
            SchemaVersion: AnalysisStateStore.SchemaVersion,
            AppVersion: "2.1.9",
            WindowsBuild: 26200,
            TimestampUtc: new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc),
            Context: null,
            Recommendations: null,
            Health: null,
            AnalysisState: "Completed",
            Warnings: [],
            Duration: TimeSpan.FromSeconds(30),
            ErrorCode: null,
            CorrelationId: "abc",
            InstalledGamesFingerprint: null,
            Display: display);

    [Fact]
    public void RoundTrip_PreservesDisplay()
    {
        var store = new AnalysisStateStore(Path.Combine(_dir, "analysis-state.json"));
        store.SaveAnalysis(SamplePersisted(Sample()));

        var loaded = store.LoadLatestAnalysis();
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.Display);
        Assert.Equal("Network: persistido", loaded.Display.Network);
        Assert.Null(loaded.Display.Security);
        Assert.Equal("1.1.1.1", loaded.Display.DnsPrimary);
        Assert.Equal(12.3, loaded.Display.DnsPrimaryMs);
        Assert.Equal("8.8.8.8", loaded.Display.DnsSecondary);
        Assert.Single(loaded.Display.DnsRows!);
        Assert.Equal(60.5, loaded.Display.StorageRows!.Single().UsedPct);
        Assert.Equal(2.5, loaded.Display.DpcMax);
        Assert.Equal("0 bloqueadas", loaded.Display.GamingScan);
    }

    [Fact]
    public void OldFile_WithoutDisplay_LoadsFine()
    {
        var path = Path.Combine(_dir, "analysis-state.json");
        File.WriteAllText(path, """
            {"SchemaVersion":3,"AppVersion":"2.1.8","WindowsBuild":26200,
             "TimestampUtc":"2026-09-01T00:00:00Z","Context":null,"Recommendations":null,
             "Health":null,"AnalysisState":"Completed","Warnings":[],"Duration":"00:00:30",
             "ErrorCode":null,"CorrelationId":null,"InstalledGamesFingerprint":null}
            """);
        var store = new AnalysisStateStore(path);

        var loaded = store.LoadLatestAnalysis();
        Assert.NotNull(loaded);
        Assert.Null(loaded.Display);
    }
}
