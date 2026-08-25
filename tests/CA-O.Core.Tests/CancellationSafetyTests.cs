using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Cancellation safety (FASE 6): cancel before APPLY aborts; cancel DURING
/// apply never leaves a half-applied mutation — the current mutation
/// finishes, verification runs, and the transaction exits in a terminal
/// well-defined state.
/// </summary>
public sealed class CancellationSafetyTests
{
    private static readonly SystemContext Context = SystemContextFactory.Default();

    private static OptimizationDefinition Definition() => new()
    {
        Id = "cancel-test",
        NameEs = "Prueba",
        NameEn = "Test",
        DescriptionEs = "Cambio para pruebas de cancelación.",
        DescriptionEn = "Cancellation test change.",
        Evidence = EvidenceLevel.Benchmark,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
    };

    /// <summary>Optimization whose Apply ignores the token and takes a fixed time.</summary>
    private sealed class SlowAtomicOptimization(OptimizationDefinition definition, int delayMs, bool failAtEnd = false) : IOptimization
    {
        /// <summary>Signals when the atomic mutation has actually begun.</summary>
        public TaskCompletionSource MutationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool MutationCompleted { get; private set; }

        public OptimizationDefinition Definition { get; } = definition;

        public OptimizationState Detect(IRegistryAccessor registry) =>
            registry.GetValue(RegistryHive2.CurrentUser, @"SOFTWARE\CA-O\Cancel", "V") is 1
                ? OptimizationState.AppliedByCao
                : OptimizationState.NotApplied;

        public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

        public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
        {
            // Atomic mutation: deliberately does NOT observe ct mid-flight.
            MutationStarted.TrySetResult();
            await Task.Delay(delayMs);
            context.Registry.SetValue(RegistryHive2.CurrentUser, @"SOFTWARE\CA-O\Cancel", "V",
                1, RegistryValueKind2.DWord);
            MutationCompleted = true;
            return failAtEnd
                ? OperationResult.Fail("falló al final del apply")
                : OperationResult.Ok("aplicado");
        }

        public Task<OperationResult> RevertAsync(OptimizationContext c, OptimizationSnapshot s, CancellationToken ct = default)
        {
            c.Registry.DeleteValue(RegistryHive2.CurrentUser, @"SOFTWARE\CA-O\Cancel", "V");
            return Task.FromResult(OperationResult.Ok("revertido"));
        }
    }

    [Fact]
    public async Task CancelBeforeApplyAbortsCleanly()
    {
        var registry = new MemoryRegistry();
        var stub = new StubOptimization(Definition());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var transaction = new OptimizationTransaction(stub, registry, Context);

        // A pre-cancelled token aborts at the first checkpoint.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transaction.RunAsync(cts.Token));
        Assert.Equal(0, stub.ApplyCalls);
        Assert.Equal(OptimizationState.NotApplied, stub.Detect(registry));
    }

    [Fact]
    public async Task CancelDuringApplyDoesNotLeaveHalfAppliedState()
    {
        var registry = new MemoryRegistry();
        var snapshots = new MemorySnapshotStore();
        var slow = new SlowAtomicOptimization(Definition(), delayMs: 150);
        using var cts = new CancellationTokenSource();

        var runTask = new OptimizationTransaction(
            slow, registry, Context, snapshots: snapshots).RunAsync(cts.Token);

        await slow.MutationStarted.Task; // cancel only once inside APPLY
        cts.Cancel();

        var report = await runTask;

        // The atomic mutation completed (cancel cannot interrupt it).
        Assert.True(slow.MutationCompleted, "mutation flag false");
        Assert.True(report.Success, $"diag: phase={report.FinalPhase} err={report.Error} msg={report.MessageEs} rolled={report.RolledBack} deferred={report.CancellationDeferred}");
        Assert.Equal(TransactionPhase.Commit, report.FinalPhase);
        Assert.True(report.CancellationDeferred);
        Assert.Equal(OptimizationState.AppliedByCao, slow.Detect(registry));
    }

    [Fact]
    public async Task CancelDuringVerifyStillRollsBackOnFailure()
    {
        var registry = new MemoryRegistry();
        var slowBroken = new SlowAtomicOptimization(Definition(), delayMs: 50, failAtEnd: true);
        using var cts = new CancellationTokenSource();

        var runTask = new OptimizationTransaction(slowBroken, registry, Context).RunAsync(cts.Token);
        cts.Cancel();

        var report = await runTask;

        Assert.False(report.Success);
        Assert.True(report.RolledBack);
        Assert.True(report.RollbackVerified);
    }
}
