using CAO.Core.Abstractions;
using CAO.Infrastructure.Logging;
using CAO.Infrastructure.Persistence;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Persistence tests: history.jsonl hash-chain (FASE 13), transaction-scoped
/// snapshots with integrity (P0-3/P2-23) and legacy v2 migration reads.
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

        var restored = Assert.Single(logger.ReadLast(10));
        Assert.Equal(entry.OptimizationId, restored.OptimizationId);
        Assert.Equal("passed", restored.Precondition);
        Assert.Equal("success", restored.ApplyResult);
        Assert.True(restored.RollbackAvailable);
    }

    [Fact]
    public void TransactionSnapshotRoundTripsThroughTxIdentity()
    {
        var store = new FileSnapshotStore(_tempDirectory);
        var txid = Guid.NewGuid();
        var state = new OptimizationSnapshot { TimestampUtc = DateTime.UtcNow.AddMinutes(-1) };
        state.Registry.Add(new RegistrySnapshotEntry(
            "CurrentUser", @"SOFTWARE\CA-O\X", "ExistedValue", 42, Existed: true)
        { Kind = RegistryValueKind2.DWord });
        state.Registry.Add(new RegistrySnapshotEntry(
            "CurrentUser", @"SOFTWARE\CA-O\X", "AbsentValue", null, Existed: false));

        store.Save(new CAO.Core.Rollback.TransactionSnapshotRecord
        {
            Manifest = new CAO.Core.Rollback.TransactionSnapshotManifest
            {
                TransactionId = txid,
                OptimizationId = "tx-opt",
                DefinitionVersion = AppVersion.Semantic,
                SchemaVersion = 3,
                AppVersion = AppVersion.Semantic,
                WindowsBuild = 26200,
                TimestampUtc = DateTime.UtcNow,
            },
            State = state,
        });

        Assert.True(store.TryLoad(txid, out var loaded));
        Assert.NotNull(loaded);

        var existed = loaded.State.Registry.Single(e => e.ValueName == "ExistedValue");
        var absent = loaded.State.Registry.Single(e => e.ValueName == "AbsentValue");
        Assert.Equal(42, existed.Value);
        Assert.Equal(RegistryValueKind2.DWord, existed.Kind);
        Assert.False(absent.Existed); // revert must DELETE this value

        // Latest-by-optimization path used by revert.
        Assert.True(store.TryLoadLatestForOptimization("tx-opt", out var latest));
        Assert.Equal(txid, latest!.Manifest.TransactionId);
    }

    [Fact]
    public void TamperedSnapshotStateFailsIntegrityCheck()
    {
        var rootDir = Path.Combine(_tempDirectory, "integ");
        Directory.CreateDirectory(rootDir);
        var store = new FileSnapshotStore(rootDir);
        var txid = Guid.NewGuid();
        store.Save(new CAO.Core.Rollback.TransactionSnapshotRecord
        {
            Manifest = new CAO.Core.Rollback.TransactionSnapshotManifest
            {
                TransactionId = txid,
                OptimizationId = "tamper",
                DefinitionVersion = "1",
                SchemaVersion = 3,
                AppVersion = "2.0.0",
                WindowsBuild = 26200,
                TimestampUtc = DateTime.UtcNow,
            },
            State = new OptimizationSnapshot(),
        });

        var stateFile = Path.Combine(rootDir, txid.ToString("D"), "snapshot.json");
        File.WriteAllText(stateFile, File.ReadAllText(stateFile).Replace("{", "{ "));

        Assert.False(store.TryLoad(txid, out _));
    }

    [Fact]
    public void LegacyFlatSnapshotsRemainReadableAndDeletable()
    {
        Directory.CreateDirectory(_tempDirectory);
        var legacyPath = Path.Combine(_tempDirectory, "legacy-opt.json");
        File.WriteAllText(legacyPath,
            """{"TimestampUtc":"2026-08-25T12:00:00Z","Registry":[],"ServiceStartTypes":[],"RawNotes":[]}""");

        var store = new FileSnapshotStore(_tempDirectory);
        Assert.True(store.TryLoadLegacy("legacy-opt", out var snapshot));
        Assert.NotNull(snapshot);
        Assert.Contains("legacy-opt", store.ListLegacyIds());

        store.DeleteLegacy("legacy-opt");
        Assert.DoesNotContain("legacy-opt", store.ListLegacyIds());
    }
}
