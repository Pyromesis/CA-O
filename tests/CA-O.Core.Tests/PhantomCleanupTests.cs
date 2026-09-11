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

    [Theory]
    [InlineData("x;calc")]
    [InlineData("")]
    public async Task ExportInvalidIdFailsWithoutElevation(string id)
    {
        var result = await Engine().ExportDriverAsync(id);
        Assert.False(result.Success);
        Assert.Equal("invalid-id", result.Error);
        Assert.Equal(string.Empty, result.Directory);
    }

    [Theory]
    [InlineData(@"HDAUDIO\FUNC_01&VEN_10EC&DEV_0283&SUBSYS_10EC0000&REV_1000\4&1234ABCD&0&0001")]
    [InlineData(@"HID\{00001812-0000-1000-8000-00805F9B34FB}_DEV_VID&0002044D_PID&000065AB\8&2D0E7D8C&0&0000")]
    [InlineData(@"STORAGE\VOLUME\{8f3b2c1a-0000-0000-0000-100000000000}\0000000000100000")]
    [InlineData("...")]
    [InlineData("   ")]
    [InlineData("a/b\\c:d")]
    public void SanitizeDeviceDir_AlwaysPassesDestPolicy(string instanceId)
    {
        var leaf = OptimizationEngine.SanitizeDeviceDir(instanceId);
        Assert.False(string.IsNullOrWhiteSpace(leaf));
        Assert.True(leaf.Length <= 80);
        Assert.DoesNotContain("..", leaf);
        var dest = System.IO.Path.Combine(
            CAO.Shared.Security.CommandPolicy.ExportDriverBackupRoot(), leaf);
        Assert.True(CAO.Shared.Security.CommandPolicy.IsExportDriverDest(dest));
    }

    [Fact]
    public async Task PresentDeviceIds_ReturnsSetWithoutThrowing()
    {
        var present = await OptimizationEngine.ReadPresentDeviceIdsAsync();
        Assert.NotNull(present);
    }
}
