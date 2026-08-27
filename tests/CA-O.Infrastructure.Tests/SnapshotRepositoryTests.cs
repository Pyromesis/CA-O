using CAO.Core.Abstractions;
using CAO.Core.Rollback;
using CAO.Infrastructure.Persistence;
using Xunit;

namespace CAO.Infrastructure.Tests;

public sealed class SnapshotRepositoryTests
{
    private static FileSnapshotStore NewStore() => new(Path.Combine(Path.GetTempPath(), $"cao-snap-{Guid.NewGuid():N}"));

    [Fact]
    public async Task Save_And_GetLatest_ReturnsSame()
    {
        var store = NewStore();
        var repo = new SnapshotRepository(store);
        var tx = Guid.NewGuid();
        var rec = new TransactionSnapshotRecord { Manifest = new TransactionSnapshotManifest { TransactionId = tx, OptimizationId = "test-opt", DefinitionVersion = "1", SchemaVersion = 3, AppVersion = "2.0.0", WindowsBuild = 26200, TimestampUtc = DateTime.UtcNow }, State = new OptimizationSnapshot() };
        store.Save(rec);
        var latest = await repo.GetLatestSnapshotForOptimizationAsync("test-opt");
        Assert.NotNull(latest);
        Assert.Equal(tx, latest!.Manifest.TransactionId);
    }

    [Fact]
    public async Task GetAll_Empty_ReturnsEmpty()
    {
        var repo = new SnapshotRepository(NewStore());
        var all = await repo.GetAllSnapshotsAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task Validate_InvalidId_ReturnsFalse()
    {
        var repo = new SnapshotRepository(NewStore());
        Assert.False(await repo.ValidateSnapshotAsync(Guid.NewGuid()));
    }
}
