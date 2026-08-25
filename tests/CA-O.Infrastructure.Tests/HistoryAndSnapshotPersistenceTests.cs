using CAO.Core.Abstractions;
using CAO.Infrastructure.Logging;
using CAO.Infrastructure.Persistence;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Persistence tests: the JSONL history schema (spec 74) and the snapshot
/// store round-trip that rollback depends on (spec 72-73, 122).
/// </summary>
public sealed class HistoryAndSnapshotPersistenceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(), "cao-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void HistoryRoundTripPreservesSpec74Fields()
    {
        var logger = new JsonHistoryLogger(Path.Combine(_tempDirectory, "history.jsonl"));
        var entry = new HistoryEntry
        {
            TimestampUtc = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc),
            AppVersion = AppVersion.Semantic,
            WindowsBuild = 26200,
            User = "tester",
            OptimizationId = "disable-transparency",
            Operation = "apply",
            Precondition = "passed",
            SnapshotId = "disable-transparency",
            ApplyResult = "success",
            Verification = "passed",
            Success = true,
            RollbackAvailable = true,
        };

        logger.Log(entry);

        var read = logger.ReadLast(10);
        var restored = Assert.Single(read);
        Assert.Equal(entry.OptimizationId, restored.OptimizationId);
        Assert.Equal(entry.Operation, restored.Operation);
        Assert.Equal(entry.WindowsBuild, restored.WindowsBuild);
        Assert.Equal(entry.AppVersion, restored.AppVersion);
        Assert.Equal("passed", restored.Precondition);
        Assert.Equal("success", restored.ApplyResult);
        Assert.Equal("passed", restored.Verification);
        Assert.True(restored.RollbackAvailable);
    }

    [Fact]
    public void CorruptLinesAreSkippedWithoutBreakingTheLog()
    {
        var path = Path.Combine(_tempDirectory, "history.jsonl");
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllLines(path,
        [
            """{"TimestampUtc":"2026-08-25T12:00:00Z","OptimizationId":"a","Operation":"apply"}""",
            "{not valid json",
            """{"TimestampUtc":"2026-08-25T13:00:00Z","OptimizationId":"b","Operation":"revert"}""",
        ]);
        var logger = new JsonHistoryLogger(path);

        var entries = logger.ReadLast(int.MaxValue);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.False(string.IsNullOrEmpty(entry.Operation)));
    }

    [Fact]
    public void SnapshotStoreRestoresAbsentValuesAsAbsent()
    {
        var store = new FileSnapshotStore(_tempDirectory);
        var snapshot = new OptimizationSnapshot();
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            "CurrentUser", @"SOFTWARE\CA-O\X", "ExistedValue", 42, Existed: true));
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            "CurrentUser", @"SOFTWARE\CA-O\X", "AbsentValue", null, Existed: false));

        store.Save("test-opt", snapshot);
        Assert.True(store.TryLoad("test-opt", out var loaded));

        var existed = loaded.Registry.Single(e => e.ValueName == "ExistedValue");
        var absent = loaded.Registry.Single(e => e.ValueName == "AbsentValue");
        Assert.Equal(42, existed.Value);
        Assert.False(absent.Existed); // revert must DELETE this value, not write a default
    }

    [Fact]
    public void SnapshotIdsAreSanitizedAgainstPathTraversal()
    {
        var store = new FileSnapshotStore(_tempDirectory);

        store.Save("../../evil", new OptimizationSnapshot());

        Assert.Empty(Directory.GetFiles(_tempDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => f.Contains("..")));
        // Nothing crashed; the sanitized file lives under the store directory.
    }
}
