using CAO.Core.Abstractions;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Proofs for the final hardening P0s:
///   P0-5  irreversible optimizations ARE verified; Unknown never passes,
///         and failure reports honestly WITHOUT automatic rollback.
///   P0-7  post-commit benchmark failure NEVER flips Success to false.
/// </summary>
public sealed class HardeningBehaviorTests
{
    private static readonly SystemContext Context = SystemContextFactory.Default();

    private static OptimizationDefinition Definition(
        bool irreversible,
        EvidenceLevel evidence = EvidenceLevel.Benchmark) => new()
    {
        Id = "hardening-" + (irreversible ? "irrev" : "rev"),
        NameEs = "Prueba",
        NameEn = "Test",
        DescriptionEs = "Prueba de hardening.",
        DescriptionEn = "Hardening test.",
        Evidence = evidence,
        Risk = irreversible ? RiskLevel.Moderate : RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Flags = irreversible ? OptimizationFlags.NotReversible : OptimizationFlags.None,
    };

    /// <summary>Maintenance-style optimization whose Detect is always Unknown.</summary>
    private sealed class UnverifiableIrreversible(OptimizationDefinition definition) : IOptimization
    {
        public int VerifyCalls { get; private set; }

        public OptimizationDefinition Definition { get; } = definition;

        public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

        public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

        public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default) =>
            Task.FromResult(OperationResult.Ok("ejecutado"));

        public Task<OperationResult> RevertAsync(OptimizationContext c, OptimizationSnapshot s, CancellationToken ct = default) =>
            Task.FromResult(OperationResult.Ok("no-op"));

        public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
        {
            VerifyCalls++;
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia verificable."));
        }
    }

    [Fact]
    public async Task IrreversibleUnknownIsVerifiedAndReportedFailedWithoutRollback()
    {
        var registry = new MemoryRegistry();
        var snapshots = new MemorySnapshotStore();
        var stub = new UnverifiableIrreversible(Definition(irreversible: true));

        var report = await new OptimizationTransaction(
            stub, registry, Context, snapshots: snapshots).RunAsync();

        // P0-5: verification RAN (not skipped).
        Assert.Equal(1, stub.VerifyCalls);

        // Unknown != Passed: transaction fails honestly...
        Assert.False(report.Success);
        Assert.Contains("CAO-VERIFY-002", report.Error);

        // ...and NO automatic rollback was attempted (impossible anyway).
        Assert.False(report.RolledBack);
        Assert.False(report.RollbackVerified);
    }

    [Fact]
    public async Task IrreversibleWithExecutionEvidencePasses()
    {
        var registry = new MemoryRegistry();
        var stub = new EvidenceBackedMaintenance();

        var report = await new OptimizationTransaction(stub, registry, Context).RunAsync();

        Assert.True(report.Success, report.MessageEs);
        Assert.Equal(TransactionPhase.Commit, report.FinalPhase);
    }

    /// <summary>P0-5 spec example: verifies via exit-code evidence.</summary>
    private sealed class EvidenceBackedMaintenance : IOptimization
    {
        private int? _lastExitCode;

        public OptimizationDefinition Definition { get; } = Definition(irreversible: true);

        public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

        public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

        public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
        {
            _lastExitCode = 0; // simulated successful external run
            return Task.FromResult(OperationResult.Ok("mantenimiento ejecutado"));
        }

        public Task<OperationResult> RevertAsync(OptimizationContext c, OptimizationSnapshot s, CancellationToken ct = default) =>
            Task.FromResult(OperationResult.Ok("no-op"));

        public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
        {
            VerificationResult result = _lastExitCode == 0
                ? VerificationResult.Passed(OptimizationState.Unknown,
                    "Operación ejecutada con exit=0 (evidencia de ejecución).")
                : VerificationResult.Unknown(OptimizationState.Unknown, "sin evidencia");
            return Task.FromResult(result);
        }
    }

    /// <summary>Benchmark hook that throws: must NOT flip Success.</summary>
    private sealed class ExplodingBenchmarkOptimization : IOptimization
    {
        public OptimizationDefinition Definition { get; } = Definition(irreversible: false);

        public OptimizationState Detect(IRegistryAccessor registry) =>
            registry.GetValue(RegistryHive2.CurrentUser, @"SOFTWARE\CA-O\Bench", "V") is 1
                ? OptimizationState.AppliedByCao
                : OptimizationState.NotApplied;

        public OptimizationSnapshot Capture(IRegistryAccessor registry)
        {
            var snap = new OptimizationSnapshot();
            snap.Registry.Add(new RegistrySnapshotEntry(
                RegistryHive2.CurrentUser.ToString(), @"SOFTWARE\CA-O\Bench", "V", null, false));
            return snap;
        }

        public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
        {
            context.Registry.SetValue(RegistryHive2.CurrentUser, @"SOFTWARE\CA-O\Bench", "V", 1, RegistryValueKind2.DWord);
            return Task.FromResult(OperationResult.Ok("aplicado"));
        }

        public Task<OperationResult> RevertAsync(OptimizationContext c, OptimizationSnapshot s, CancellationToken ct = default)
        {
            c.Registry.DeleteValue(RegistryHive2.CurrentUser, @"SOFTWARE\CA-O\Bench", "V");
            return Task.FromResult(OperationResult.Ok("revertido"));
        }

        public Task<BenchmarkResult?> BenchmarkAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("benchmark explotó a propósito");
    }

    [Fact]
    public async Task BenchmarkFailureDoesNotInvalidateCommittedChange()
    {
        var registry = new MemoryRegistry();
        var boom = new ExplodingBenchmarkOptimization();

        var report = await new OptimizationTransaction(
            boom, registry, Context, snapshots: new MemorySnapshotStore()).RunAsync();

        // P0-7 core guarantee:
        Assert.True(report.Success);
        Assert.Equal(TransactionPhase.Commit, report.FinalPhase);
        Assert.Null(report.Benchmark);
        Assert.NotNull(report.BenchmarkError);
        Assert.Contains("explotó", report.BenchmarkError);
    }
}
