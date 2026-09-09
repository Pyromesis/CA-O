using CAO.Core.Abstractions;
using CAO.Core.Engine;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Fase drivers 2: validación de entrada y parseo de verificación de FixDriver.
/// El camino con éxito requiere elevación real y se verifica en integración.
/// </summary>
public sealed class DriverFixTests
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

    [Theory]
    [InlineData("bogus")]
    [InlineData("RESCAN")]
    [InlineData("")]
    public async Task InvalidActionFailsWithoutElevation(string action)
    {
        var result = await Engine().FixDriverAsync(RealId, action);
        Assert.False(result.Success);
        Assert.Equal("invalid-action", result.Error);
    }

    [Theory]
    [InlineData("x;calc")]
    [InlineData("con espacios")]
    [InlineData("")]
    public async Task InvalidInstanceIdFailsWithoutElevation(string instanceId)
    {
        var result = await Engine().FixDriverAsync(instanceId, "rescan");
        Assert.False(result.Success);
        Assert.Equal("invalid-id", result.Error);
    }

    [Theory]
    [InlineData(@"C:\x\drv.exe")]
    [InlineData(@"C:\x\..\drv.inf")]
    [InlineData("relativo.inf")]
    public async Task InvalidInfPathFailsWithoutElevation(string inf)
    {
        var result = await Engine().InstallDriverInfAsync(inf, string.Empty);
        Assert.False(result.Success);
        Assert.Equal("invalid-inf", result.Error);
    }

    [Fact]
    public void ProblemListParsingFindsExactInstance()
    {
        const string output = """
            Microsoft PnP Utility

            Instance ID:                HDAUDIO\FUNC_01&VEN_10EC&DEV_0283&SUBSYS_10EC0000&REV_1000\4&1234ABCD&0&0001
            Device Description:         Altavoces
            Class Name:                 MEDIA
            Status:                     Started
            Problem:                    CM_PROB_FAILED_START

            Instance ID:                USB\VID_046D&PID_C52B\5&1A2B3C4D&0&1
            Device Description:         USB Receiver
            Status:                     Started
            Problem:                    CM_PROB_DEVICE_NOT_THERE
            """;
        Assert.True(OptimizationEngine.IsProblemListed(output, RealId));
        Assert.True(OptimizationEngine.IsProblemListed(output.ToLowerInvariant(), RealId));
        Assert.False(OptimizationEngine.IsProblemListed(output, @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0283&SUBSYS_10EC0000&REV_1000\4&1234ABCD&0&0002"));
        Assert.False(OptimizationEngine.IsProblemListed("sin dispositivos", RealId));
        Assert.False(OptimizationEngine.IsProblemListed(string.Empty, RealId));
    }
}
