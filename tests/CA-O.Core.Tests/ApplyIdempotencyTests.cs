using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Idempotencia de Apply: una optimización ya aplicada no se puede volver a
/// activar. El reintento devuelve éxito informativo SIN mutar ni crear
/// snapshot nuevo (el Detect manda, no la tarjeta de la UI).
/// </summary>
public sealed class ApplyIdempotencyTests
{
    private static readonly SystemContext Context = SystemContextFactory.Default();

    private static OptimizationDefinition Definition(string id = "stub-idempotent") => new()
    {
        Id = id,
        NameEs = "Prueba",
        NameEn = "Test",
        DescriptionEs = "Cambio de prueba de idempotencia",
        DescriptionEn = "Idempotency test change",
        Evidence = EvidenceLevel.Benchmark,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
    };

    [Fact]
    public async Task AlreadyApplied_SkipsMutation_WithoutNewSnapshot()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser, StubOptimization.TestKey,
            StubOptimization.TestValue, 1, RegistryValueKind2.DWord);
        var snapshots = new MemorySnapshotStore();
        var history = new MemoryHistory();
        var stub = new StubOptimization(Definition());
        Assert.Equal(OptimizationState.AppliedByCao, stub.Detect(registry));

        var report = await new OptimizationTransaction(
            stub, registry, Context, snapshots: snapshots, history: history).RunAsync();

        Assert.True(report.Success);
        Assert.Equal(TransactionPhase.Commit, report.FinalPhase);
        Assert.Contains("Ya aplicado", report.MessageEs, StringComparison.Ordinal);
        Assert.Equal(0, stub.ApplyCalls);
        Assert.Empty(snapshots.Saved);
        Assert.Contains(history.Entries, e =>
            e.Operation == "apply" && e.Success && e.ApplyResult == "skipped-already-applied");
    }

    [Fact]
    public async Task NotApplied_RunsFullTransaction_AndSecondRunSkips()
    {
        var registry = new MemoryRegistry();
        var snapshots = new MemorySnapshotStore();
        var stub = new StubOptimization(Definition());

        var first = await new OptimizationTransaction(
            stub, registry, Context, snapshots: snapshots).RunAsync();

        Assert.True(first.Success);
        Assert.Equal(1, stub.ApplyCalls);
        Assert.Single(snapshots.Saved);
        Assert.Equal(OptimizationState.AppliedByCao, stub.Detect(registry));

        var second = await new OptimizationTransaction(
            stub, registry, Context, snapshots: snapshots).RunAsync();

        Assert.True(second.Success);
        Assert.Contains("Ya aplicado", second.MessageEs, StringComparison.Ordinal);
        Assert.Equal(1, stub.ApplyCalls);
        Assert.Single(snapshots.Saved);
    }
}
