using CAO.Infrastructure.Persistence;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>Persistencia analysis-state §79: save/load/corrupt/version/atómico.</summary>
public sealed class AnalysisStateStoreTests
{
    private static AnalysisStateStore NewStore() => new(Path.Combine(Path.GetTempPath(), $"cao-analysis-{Guid.NewGuid():N}.json"));

    [Fact]
    public void Save_And_Load_RoundTrips()
    {
        var store = NewStore();
        var ctx = SystemContextFactory.Default();
        var rec = new AnalysisStateStore.PersistedAnalysis(AnalysisStateStore.SchemaVersion, "2.0.0", 26200, DateTime.UtcNow, ctx, Array.Empty<Recommendation>(), null, "Completed", Array.Empty<string>(), TimeSpan.FromSeconds(5), null, "corr1");
        store.SaveAnalysis(rec);
        Assert.True(store.HasAnalysis());
        var loaded = store.LoadLatestAnalysis();
        Assert.NotNull(loaded);
        Assert.Equal("Completed", loaded!.AnalysisState);
        Assert.Equal(ctx.WindowsBuild, loaded.Context!.WindowsBuild);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsNullAndQuarantines()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cao-corrupt-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json [");
        var store = new AnalysisStateStore(path);
        var loaded = store.LoadLatestAnalysis();
        Assert.Null(loaded);
        // Archivo original movido a .corrupt.* y HasAnalysis false hasta próximo Save
        Assert.False(File.Exists(path));
        Assert.True(Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + ".corrupt.*").Length >= 1);
    }

    [Fact]
    public void Load_WrongSchemaVersion_ReturnsNull()
    {
        var store = NewStore();
        var ctx = SystemContextFactory.Default();
        var rec = new AnalysisStateStore.PersistedAnalysis(999, "2.0.0", 26200, DateTime.UtcNow, ctx, null, null, "Completed", Array.Empty<string>(), TimeSpan.Zero, null, null);
        // Guardar con schema viejo simulando versión antigua: escribir directo JSON con SchemaVersion 999
        var json = System.Text.Json.JsonSerializer.Serialize(rec);
        var field = typeof(AnalysisStateStore).GetField("_filePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var file = (string)field.GetValue(store)!;
        File.WriteAllText(file, json);
        Assert.Null(store.LoadLatestAnalysis());
    }

    [Fact]
    public void IsObsolete_DetectsAgeAndBuildChange()
    {
        var store = NewStore();
        var ctx = SystemContextFactory.Default();
        var old = new AnalysisStateStore.PersistedAnalysis(AnalysisStateStore.SchemaVersion, "2.0.0", 26200, DateTime.UtcNow.AddHours(-25), ctx, null, null, "Completed", Array.Empty<string>(), TimeSpan.Zero, null, null);
        Assert.True(store.IsObsolete(old));
        var sameBuild = new AnalysisStateStore.PersistedAnalysis(AnalysisStateStore.SchemaVersion, "2.0.0", 26200, DateTime.UtcNow, ctx, null, null, "Completed", Array.Empty<string>(), TimeSpan.Zero, null, null);
        var newCtx = ctx with { WindowsBuild = 99999 };
        Assert.True(store.IsObsolete(sameBuild, newCtx));
        Assert.False(store.IsObsolete(sameBuild, ctx));
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        var store = NewStore();
        var ctx = SystemContextFactory.Default();
        store.SaveAnalysis(new AnalysisStateStore.PersistedAnalysis(AnalysisStateStore.SchemaVersion, "2.0.0", 26200, DateTime.UtcNow, ctx, null, null, "Completed", Array.Empty<string>(), TimeSpan.Zero, null, null));
        Assert.True(store.HasAnalysis());
        store.DeleteAnalysis();
        Assert.False(store.HasAnalysis());
    }
}
