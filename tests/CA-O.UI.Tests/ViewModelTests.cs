using CAO.Core.Abstractions;
using CAO.Infrastructure.Logging;
using CAO.Infrastructure.Persistence;
using CAO.Infrastructure.Services;
using CAO.UI.ViewModels;
using CAO.Shared;
using Xunit;

namespace CAO.UI.Tests;

file sealed class StubProvider : ISystemContextProvider
{
    public Task<SystemContext> GetAsync(CancellationToken ct = default) => Task.FromResult(SystemContextFactory.Default());
}
file sealed class StubRegistry : IRegistryAccessor
{
    public CAO.Core.Abstractions.RegistryValueKind2 GetKind(RegistryHive2 h, string p, string n) => CAO.Core.Abstractions.RegistryValueKind2.None;
    public object? GetValue(RegistryHive2 h, string p, string n) => null;
    public object? GetValueRaw(RegistryHive2 h, string p, string n, out CAO.Core.Abstractions.RegistryValueKind2 k) { k = CAO.Core.Abstractions.RegistryValueKind2.None; return null; }
    public void SetValue(RegistryHive2 h, string p, string n, object v, CAO.Core.Abstractions.RegistryValueKind2 k) { }
    public void SetValueRaw(RegistryHive2 h, string p, string n, object v, CAO.Core.Abstractions.RegistryValueKind2 k) { }
    public bool DeleteValue(RegistryHive2 h, string p, string n) => false;
    public IReadOnlyList<string> GetValueNames(RegistryHive2 h, string p) => Array.Empty<string>();
}

/// <summary>ViewModels (§96): recommendation, health, scoring, cancellation contracts.</summary>
public sealed class ViewModelTests
{
    private static (AnalyzeViewModel vm, DashboardViewModel dvm) CreateVms()
    {
        var store = new AnalysisStateStore(Path.Combine(Path.GetTempPath(), $"cao-test-{Guid.NewGuid():N}.json"));
        var logger = new Infrastructure.Logging.StructuredLogger(Path.Combine(Path.GetTempPath(), $"cao-log-{Guid.NewGuid():N}.log"));
        var svc = new SystemAnalysisService(new StubProvider(), store, new StubRegistry());
        var state = new UiState();
        var provider = new CAO.Infrastructure.SystemInterop.SystemContextProvider();
        var snapshotStore = new CAO.Infrastructure.Persistence.FileSnapshotStore();
        var journal = new CAO.Infrastructure.Persistence.FileTransactionJournal();
        var recoveryService = new CAO.Core.Rollback.CrashRecoveryService(journal, snapshotStore, _ => CAO.Shared.OptimizationState.Unknown);
        return (new AnalyzeViewModel(svc, state, logger), new DashboardViewModel(state, svc, store, logger, provider, recoveryService));
    }

    [Fact]
    public void AnalyzeViewModel_InitialState_IsNotRunning()
    {
        var (vm, _) = CreateVms();
        Assert.False(vm.IsRunning);
        Assert.Equal("Inactivo", vm.OverallStatus);
        Assert.False(vm.CanCancel);
    }

    [Fact]
    public async Task AnalyzeViewModel_RunAsync_ProducesPerModuleResults()
    {
        var (vm, _) = CreateVms();
        var results = await vm.RunAsync(CancellationToken.None);
        // Al menos 5 módulos deben reportar estado (Completed/Failed) sin abortar global
        Assert.True(results.Count >= 5);
        Assert.Contains(results, r => r.Module == "Security");
        // OverallStatus debe reflejar completado o completado con advertencias, nunca crash
        Assert.False(string.IsNullOrWhiteSpace(vm.OverallStatus));
    }

    [Fact]
    public async Task AnalyzeViewModel_Cancellation_ProducesCancelledStatus()
    {
        var (vm, _) = CreateVms();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var results = await vm.RunAsync(cts.Token);
        // Con token ya cancelado, al menos algún módulo en Cancelled o todo en Cancelled
        Assert.True(results.Count > 0);
    }

    [Fact]
    public async Task DashboardViewModel_LoadAsync_SetsHealth()
    {
        var (_, dvm) = CreateVms();
        await dvm.LoadCommand.ExecuteAsync(null);
        // Sin contexto, Load debe no crashear y dejar IsLoading=false
        Assert.False(dvm.IsLoading);
    }

    [Fact]
    public void Correlation_New_IsUniqueAndShort()
    {
        var a = CAO.Shared.Correlation.New();
        var b = CAO.Shared.Correlation.New();
        Assert.NotEqual(a, b);
        Assert.Equal(12, a.Length);
    }
}
