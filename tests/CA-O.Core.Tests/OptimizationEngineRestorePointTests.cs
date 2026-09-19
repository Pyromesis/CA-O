using CAO.Core.Abstractions;
using CAO.Core.Engine;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// FASE 12 (fix restore-point): una operación NotReversible con
/// RequiresRestorePoint no puede aplicarse si el punto de restauración falla
/// (SR deshabilitado, disco lleno, WMI roto...). El camino reversible mantiene
/// su warning oportunista; el irreversible aborta con "no-restore-point".
/// </summary>
public sealed class OptimizationEngineRestorePointTests
{
    private sealed class FailingRestorePoints : IRestorePointService
    {
        public Task<(bool Success, string ReasonEs)> CreateAsync(string description, CancellationToken ct = default) =>
            Task.FromResult((false, "System Restore deshabilitado (simulado)."));

        public Task<IReadOnlyList<CAO.Shared.RestorePointInfo>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CAO.Shared.RestorePointInfo>>(Array.Empty<CAO.Shared.RestorePointInfo>());
    }

    [Fact]
    public async Task Irreversible_Aborts_When_Restore_Point_Creation_Fails()
    {
        var engine = new OptimizationEngine(
            new MemoryRegistry(),
            new FailingRestorePoints(),
            new MemorySnapshotStore(),
            new MemoryHistory(),
            isRunningAsAdmin: () => true);

        // windows-component-store-resetbase: NotReversible + ExpertOnly + RequiresRestorePoint.
        var result = await engine.ApplyAsync("windows-component-store-resetbase");

        Assert.False(result.Success);
        Assert.Equal("no-restore-point", result.Error);
    }
}
