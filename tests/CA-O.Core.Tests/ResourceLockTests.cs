using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Resource locking (FASE 15): two concurrent transactions over the same
/// optimization id are serialized — the mutation never overlaps.
/// </summary>
public sealed class ResourceLockTests
{
    private static int _running;
    private static int _maxRunning;

    private static OptimizationDefinition Definition(string id) => new()
    {
        Id = id,
        NameEs = "Lock test",
        NameEn = "Lock test",
        DescriptionEs = "Prueba de exclusión por recurso.",
        DescriptionEn = "Resource exclusion test.",
        Evidence = EvidenceLevel.Benchmark,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
    };

    private sealed class TrackedOptimization(OptimizationDefinition definition) : IOptimization
    {
        public OptimizationDefinition Definition { get; } = definition;

        public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

        public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

        public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
        {
            var current = Interlocked.Increment(ref _running);
            _maxRunning = Math.Max(_maxRunning, current);
            await Task.Delay(80);
            Interlocked.Decrement(ref _running);
            return OperationResult.Ok("ok");
        }

        public Task<OperationResult> RevertAsync(
            OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
            Task.FromResult(OperationResult.Ok("revertido"));
    }

    [Fact]
    public async void ConcurrentSameKeyTransactionsNeverOverlap()
    {
        var context = SystemContextFactory.Default();
        var first = new TrackedOptimization(Definition("lock-test"));
        var second = new TrackedOptimization(Definition("lock-test"));

        var t1 = new OptimizationTransaction(first, new MemoryRegistry(), context).RunAsync();
        var t2 = new OptimizationTransaction(second, new MemoryRegistry(), context).RunAsync();
        await Task.WhenAll(t1, t2);

        Assert.True(_maxRunning <= 1, $"Solapamiento detectado: {_maxRunning} mutaciones simultáneas.");
    }
}
