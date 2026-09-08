using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Core.Interfaces;
using CAO.Core.Rollback;
using CAO.Shared;
using CAO.Shared.Security;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Robustez de VERIFY: Unknown transitorio se reintenta; irreversible con
/// Unknown persistente es éxito con aviso (no "Rechazado" de un cambio real);
/// reversible con Unknown persistente sigue revirtiendo. Además: un ajuste
/// de energía ilegible falla ANTES de mutar (not-supported).
/// </summary>
public sealed class VerifyRecoveryTests
{
    private static readonly SystemContext Context = SystemContextFactory.Default();

    private static OptimizationDefinition Definition(string id, bool reversible = true) => new()
    {
        Id = id,
        NameEs = "Prueba",
        NameEn = "Test",
        DescriptionEs = "Cambio de prueba",
        DescriptionEn = "Test change",
        Evidence = EvidenceLevel.Benchmark,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Reversible = reversible,
        Flags = reversible ? OptimizationFlags.None : OptimizationFlags.NotReversible,
    };

    /// <summary>Optimización con Verify guionizable y Apply que escribe 1.</summary>
    private sealed class ScriptVerifyOptimization(OptimizationDefinition definition) : IOptimization
    {
        public OptimizationDefinition Definition { get; } = definition;
        public Func<int, VerificationResult>? VerifyScript { get; set; }
        public int VerifyCalls { get; private set; }
        public int ApplyCalls { get; private set; }

        public OptimizationState Detect(IRegistryAccessor registry)
        {
            var value = registry.GetValue(RegistryHive2.CurrentUser, StubOptimization.TestKey, StubOptimization.TestValue);
            if (value is null) return OptimizationState.NotApplied;
            return Equals(value, 1) ? OptimizationState.AppliedByCao : OptimizationState.AppliedManually;
        }

        public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

        public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
        {
            ApplyCalls++;
            context.Registry.SetValue(RegistryHive2.CurrentUser, StubOptimization.TestKey,
                StubOptimization.TestValue, 1, RegistryValueKind2.DWord);
            return Task.FromResult(OperationResult.Ok("aplicado"));
        }

        public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
        {
            context.Registry.DeleteValue(RegistryHive2.CurrentUser, StubOptimization.TestKey, StubOptimization.TestValue);
            return Task.FromResult(OperationResult.Ok("revertido"));
        }

        public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
        {
            VerifyCalls++;
            if (VerifyScript is not null) return Task.FromResult(VerifyScript(VerifyCalls));
            var observed = Detect(context.Registry);
            return Task.FromResult(observed switch
            {
                OptimizationState.AppliedByCao => VerificationResult.Passed(observed, "ok"),
                _ => VerificationResult.Failed(observed, "no refleja"),
            });
        }
    }

    private sealed class ScriptExecutor(Func<SystemCommandKey, PrivilegedCommandResult> script) : IPrivilegedCommandExecutor
    {
        public List<SystemCommandKey> Calls { get; } = [];

        public Task<PrivilegedCommandResult> ExecuteAsync(SystemCommandKey key, IReadOnlyList<string> arguments, CancellationToken ct = default)
        {
            Calls.Add(key);
            return Task.FromResult(script(key));
        }
    }

    [Fact]
    public async Task TransientUnknown_Retries_ThenCommits()
    {
        var stub = new ScriptVerifyOptimization(Definition("verify-retry"))
        {
            VerifyScript = call => call < 3
                ? VerificationResult.Unknown(OptimizationState.Unknown, "hipo de lectura")
                : VerificationResult.Passed(OptimizationState.AppliedByCao, "ok tras reintento"),
        };

        var report = await new OptimizationTransaction(
            stub, new MemoryRegistry(), Context, snapshots: new MemorySnapshotStore()).RunAsync();

        Assert.True(report.Success);
        Assert.Equal(TransactionPhase.Commit, report.FinalPhase);
        Assert.Equal(3, stub.VerifyCalls);
        Assert.Equal(1, stub.ApplyCalls);
    }

    [Fact]
    public async Task PersistentUnknown_Irreversible_SucceedsWithWarning_NoRollback()
    {
        var snapshots = new MemorySnapshotStore();
        var stub = new ScriptVerifyOptimization(Definition("verify-unknown-irreversible", reversible: false))
        {
            VerifyScript = _ => VerificationResult.Unknown(OptimizationState.Unknown, "sin lectura"),
        };

        var report = await new OptimizationTransaction(
            stub, new MemoryRegistry(), Context, snapshots: snapshots).RunAsync();

        Assert.True(report.Success);
        Assert.False(report.RolledBack);
        Assert.Contains("concluyente", report.MessageEs, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, stub.ApplyCalls);
    }

    [Fact]
    public async Task PersistentUnknown_Reversible_StillRollsBack()
    {
        var stub = new ScriptVerifyOptimization(Definition("verify-unknown-reversible"))
        {
            VerifyScript = _ => VerificationResult.Unknown(OptimizationState.Unknown, "sin lectura"),
        };

        var report = await new OptimizationTransaction(
            stub, new MemoryRegistry(), Context, snapshots: new MemorySnapshotStore()).RunAsync();

        Assert.False(report.Success);
        Assert.True(report.RolledBack);
    }

    [Fact]
    public async Task PowerAcSetting_Unreadable_FailsBeforeMutating()
    {
        var executor = new ScriptExecutor(_ => new PrivilegedCommandResult(1, string.Empty, "sin datos", TimedOut: false));
        var opt = new CAO.Core.Optimizations.Power.SetWirelessAdapterMaxPerformanceAc();
        var context = new OptimizationContext { Registry = new MemoryRegistry(), Executor = executor };

        var result = await opt.ApplyAsync(context);

        Assert.False(result.Success);
        Assert.Contains("not-supported", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SystemCommandKey.PowerCfgSetAcValueIndex, executor.Calls);
    }
}
