using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Transactional model tests (spec 122-124): PRECHECK -> SNAPSHOT -> APPLY ->
/// VERIFY -> COMMIT with rollback on failure, crash-safe snapshot
/// persistence and batch stop-on-first-failure semantics.
/// </summary>
public sealed class OptimizationTransactionTests
{
    private static readonly SystemContext Context = SystemContextFactory.Default();

    private static OptimizationDefinition Definition(
        string id = "stub-optimization",
        RiskLevel risk = RiskLevel.Safe,
        bool reversible = true) => new()
    {
        Id = id,
        NameEs = "Prueba",
        NameEn = "Test",
        DescriptionEs = "Cambio de prueba transaccional",
        DescriptionEn = "Transactional test change",
        Evidence = EvidenceLevel.Benchmark,
        Risk = risk,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Reversible = reversible,
        Flags = reversible ? OptimizationFlags.None : OptimizationFlags.NotReversible,
    };

    [Fact]
    public async Task FailedPreconditionPreventsApply()
    {
        var registry = new MemoryRegistry();
        var stub = new StubOptimization(Definition())
        {
            PreconditionOverride = _ => PreconditionResult.Fail("no aplica"),
        };
        var history = new MemoryHistory();

        var report = await RunAsync(stub, registry, history);

        Assert.False(report.Success);
        Assert.Equal(TransactionPhase.Failed, report.FinalPhase);
        Assert.Equal(0, stub.ApplyCalls);
        Assert.Contains(history.Entries, e => e.Operation == "precheck" && !e.Success && e.Precondition == "failed");
    }

    [Fact]
    public async Task SuccessfulRunReachesCommitAndPersistsSnapshot()
    {
        var registry = new MemoryRegistry();
        var snapshots = new MemorySnapshotStore();
        var stub = new StubOptimization(Definition());

        var report = await RunAsync(stub, registry, new MemoryHistory(), snapshots);

        Assert.True(report.Success);
        Assert.Equal(TransactionPhase.Commit, report.FinalPhase);
        Assert.True(snapshots.Saved.ContainsKey(Definition().Id));
        // Apply wrote the value; verification observed it.
        Assert.Equal(OptimizationState.AppliedByCao, stub.Detect(registry));
    }

    [Fact]
    public async Task SnapshotIsPersistedBeforeMutation()
    {
        var registry = new MemoryRegistry();
        var snapshots = new MemorySnapshotStore();
        var savedBeforeApply = false;

        var stub = new StubOptimization(Definition())
        {
            ApplyScript = _ =>
            {
                savedBeforeApply = snapshots.Saved.ContainsKey("stub-optimization");
                return OperationResult.Fail("boom", "scripted");
            },
        };

        var report = await RunAsync(stub, registry, new MemoryHistory(), snapshots);

        Assert.False(report.Success);
        Assert.True(savedBeforeApply, "el snapshot debe existir antes de mutar (crash-safe).");
        Assert.True(report.RolledBack);
    }

    [Fact]
    public async Task ApplyFailureTriggersRollbackToCapturedState()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser, StubOptimization.TestKey, StubOptimization.TestValue,
            0, RegistryValueKind2.DWord); // pre-existing value that must be restored

        var stub = new StubOptimization(Definition())
        {
            ApplyScript = memory =>
            {
                memory.SetValue(RegistryHive2.CurrentUser, StubOptimization.TestKey, StubOptimization.TestValue,
                    1, RegistryValueKind2.DWord);
                return OperationResult.Fail("falló a mitad del apply", "scripted");
            },
        };

        var report = await RunAsync(stub, registry, new MemoryHistory());

        Assert.False(report.Success);
        Assert.Equal(TransactionPhase.RolledBack, report.FinalPhase);
        Assert.Equal(1, stub.RevertCalls);
        Assert.Equal(0, registry.GetValue(RegistryHive2.CurrentUser, StubOptimization.TestKey, StubOptimization.TestValue));
    }

    [Fact]
    public async Task VerificationFailureTriggersRollback()
    {
        var registry = new MemoryRegistry();
        var stub = new StubOptimization(Definition())
        {
            // Apply claims success but does not write: verification must catch it.
            ApplyScript = _ => OperationResult.Ok("dijo éxito sin aplicar"),
            HonestVerification = true,
        };

        var report = await RunAsync(stub, registry, new MemoryHistory());

        Assert.False(report.Success);
        Assert.Equal(TransactionPhase.RolledBack, report.FinalPhase);
    }

    [Fact]
    public async Task MaintenanceActionsSkipVerificationButStillSucceed()
    {
        var registry = new MemoryRegistry();
        var stub = new StubOptimization(Definition(reversible: false));

        var report = await RunAsync(stub, registry, new MemoryHistory());

        Assert.True(report.Success);
        Assert.Equal(TransactionPhase.Commit, report.FinalPhase);
    }

    [Fact]
    public async Task BatchStopsOnFirstFailureAndRollsBackCommittedSafeChanges()
    {
        var registry = new MemoryRegistry();
        var snapshots = new MemorySnapshotStore();

        var good1 = new StubOptimization(Definition("batch-good-1"));
        var failing = new StubOptimization(Definition("batch-failing"))
        {
            ApplyScript = _ => OperationResult.Fail("crítico falló"),
        };
        var neverReached = new StubOptimization(Definition("batch-never"));

        var multi = new MultiOptimizationTransaction(
            [good1, failing, neverReached],
            registry,
            Context,
            snapshots: snapshots);

        var reports = await multi.RunAsync();

        Assert.Equal(2, reports.Count);
        Assert.True(reports[0].Success);
        Assert.False(reports[1].Success);
        // good1 was rolled back automatically (Safe risk) after the failure.
        Assert.Equal(OptimizationState.NotApplied, good1.Detect(registry));
        Assert.Equal(0, neverReached.ApplyCalls);
    }

    private static Task<TransactionReport> RunAsync(
        StubOptimization stub,
        MemoryRegistry registry,
        IHistoryLogger? history = null,
        ISnapshotStore? snapshots = null) =>
        new OptimizationTransaction(stub, registry, Context, snapshots: snapshots, history: history).RunAsync();
}
