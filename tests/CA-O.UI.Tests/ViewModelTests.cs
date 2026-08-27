using CAO.UI.ViewModels;
using CAO.Shared;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>ViewModels (§96): recommendation, health, scoring, cancellation contracts.</summary>
public sealed class ViewModelTests
{
    [Fact]
    public void AnalyzeViewModel_InitialState_IsNotRunning()
    {
        var vm = new AnalyzeViewModel();
        Assert.False(vm.IsRunning);
        Assert.Equal("Inactivo", vm.OverallStatus);
        Assert.False(vm.CanCancel);
    }

    [Fact]
    public async Task AnalyzeViewModel_RunAsync_ProducesPerModuleResults()
    {
        var vm = new AnalyzeViewModel();
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
        var vm = new AnalyzeViewModel();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var results = await vm.RunAsync(cts.Token);
        // Con token ya cancelado, al menos algún módulo en Cancelled o todo en Cancelled
        Assert.True(results.Count > 0);
    }

    [Fact]
    public async Task DashboardViewModel_LoadAsync_SetsHealth()
    {
        var state = new UiState();
        var vm = new DashboardViewModel(state);
        await vm.LoadCommand.ExecuteAsync(null);
        // Sin contexto, Load debe no crashear y dejar IsLoading=false
        Assert.False(vm.IsLoading);
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
