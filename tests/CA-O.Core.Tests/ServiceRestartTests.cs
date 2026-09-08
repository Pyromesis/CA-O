using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Troubleshoot;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Solucionadores por reinicio de servicio: orden stop→start, respeto a
/// servicios deshabilitados a propósito y fallo honesto si no existe.
/// </summary>
public sealed class ServiceRestartTests
{
    private sealed class FakeServices : IServiceManager
    {
        public List<string> Calls { get; } = [];
        public string StartType { get; set; } = "Automatic";
        public bool ExistsResult { get; set; } = true;

        public string? GetStartType(string serviceName) => StartType;
        public void SetStartType(string serviceName, string startType) => Calls.Add("set:" + serviceName);
        public Task StopAsync(string serviceName, CancellationToken ct = default)
        {
            Calls.Add("stop:" + serviceName);
            return Task.CompletedTask;
        }
        public Task StartAsync(string serviceName, CancellationToken ct = default)
        {
            Calls.Add("start:" + serviceName);
            return Task.CompletedTask;
        }
        public bool Exists(string serviceName) => ExistsResult;
    }

    private static OptimizationContext Context(FakeServices services) =>
        new() { Registry = new MemoryRegistry(), Services = services };

    [Fact]
    public async Task PrintSpooler_Restarts_StopThenStart()
    {
        var services = new FakeServices();
        var result = await new RestartPrintSpooler().ApplyAsync(Context(services));

        Assert.True(result.Success);
        Assert.Equal(["stop:Spooler", "start:Spooler"], services.Calls);
        var verify = await new RestartPrintSpooler().VerifyAsync(Context(services));
        Assert.Equal(VerificationStatus.Passed, verify.Status);
    }

    [Fact]
    public async Task DisabledService_IsLeftAlone()
    {
        var services = new FakeServices { StartType = "Disabled" };
        var result = await new RestartBluetoothService().ApplyAsync(Context(services));

        Assert.False(result.Success);
        Assert.Contains("service-disabled", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(services.Calls);
    }

    [Fact]
    public async Task MissingService_FailsHonestly()
    {
        var services = new FakeServices { ExistsResult = false };
        var result = await new RestartDnsClient().ApplyAsync(Context(services));

        Assert.False(result.Success);
        Assert.Contains("service-not-found", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("start:Dnscache", services.Calls);
    }
}
