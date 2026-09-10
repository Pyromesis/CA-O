using CAO.Core.Abstractions;
using CAO.Core.Engine;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Limpieza de fantasmas: validación estricta antes que privilegios (testeable
/// sin elevación) + parseo fail-safe del estado pnputil. El camino con éxito
/// requiere admin real y se verifica en integración.
/// </summary>
public sealed class PhantomCleanupTests
{
    private const string RealId = @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0283&SUBSYS_10EC0000&REV_1000\4&1234ABCD&0&0001";

    private sealed class NoRestorePoints : IRestorePointService
    {
        public Task<(bool Success, string ReasonEs)> CreateAsync(string description, CancellationToken ct = default) =>
            Task.FromResult((false, "tests"));

        public Task<IReadOnlyList<CAO.Shared.RestorePointInfo>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CAO.Shared.RestorePointInfo>>(Array.Empty<CAO.Shared.RestorePointInfo>());
    }

    private static OptimizationEngine Engine() => new(
        new MemoryRegistry(), new NoRestorePoints(), new MemorySnapshotStore(), new MemoryHistory());

    [Fact]
    public async Task EmptyListFailsWithoutElevation()
    {
        var result = await Engine().RemovePhantomDevicesAsync(Array.Empty<string>());
        Assert.False(result.Success);
        Assert.Equal("invalid-list", result.Error);
    }

    [Fact]
    public async Task OversizedListFailsWithoutElevation()
    {
        var ids = Enumerable.Repeat(RealId, 201).ToList();
        var result = await Engine().RemovePhantomDevicesAsync(ids);
        Assert.False(result.Success);
        Assert.Equal("invalid-list", result.Error);
    }

    [Theory]
    [InlineData("x;calc")]
    [InlineData("con espacios")]
    public async Task InvalidIdFailsWithoutElevation(string id)
    {
        var result = await Engine().RemovePhantomDevicesAsync(new[] { id });
        Assert.False(result.Success);
        Assert.Equal("invalid-id", result.Error);
    }

    [Theory]
    [InlineData("Status:                     Started", true)]
    [InlineData("status: started", true)]
    [InlineData("Status:                     Unknown", false)]
    [InlineData("Status:                     Stopped", false)]
    [InlineData("No devices found.", false)]
    [InlineData("", false)]
    public void IsStartedStatus_OnlyExplicitStartedCounts(string output, bool expected)
    {
        Assert.Equal(expected, OptimizationEngine.IsStartedStatus(output));
    }

    [Fact]
    public async Task PresentDeviceIds_ReturnsSetWithoutThrowing()
    {
        var present = await OptimizationEngine.ReadPresentDeviceIdsAsync();
        Assert.NotNull(present);
    }
}
